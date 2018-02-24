using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;

namespace LikeLionQuestionCrawl
{
    public partial class Form1 : Form
    {
        CookieContainer cookieContainer = new CookieContainer();

        private const string EMAIL = "";
        private const string PASSWORD = "";
        private const string SAVEPATH = @"";

        public Form1()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            Login();
        }

        private void Login()
        {
            string GetHtml = getHTMLFromWeb("https://apply.likelion.org/users/sign_in");
            string authToken = ExtractBetweenText(GetHtml, "name=\"authenticity_token\" value=\"", "\"");

            string data = "utf8=%E2%9C%93&authenticity_token=" + HttpUtility.UrlEncode(authToken) + "&" + HttpUtility.UrlEncode("user[email]") + "=" + HttpUtility.UrlEncode(EMAIL) + "&" + HttpUtility.UrlEncode("user[password]") + "=" + HttpUtility.UrlEncode(PASSWORD) + "&commit=%ED%99%95%EC%9D%B8";

            var responseString = postData("https://apply.likelion.org/users/sign_in", data);

            if (responseString.Contains("가입 정보 수정"))
            {
                MessageBox.Show("로그인 성공!");
                GetSchoolList();
            }
            else
            {
                MessageBox.Show("로그인 실패!");
            }
        }

        private async void GetSchoolList()
        {
            string GetHtml = getHTMLFromWeb("https://apply.likelion.org/users/edit");

            string[] univs = ExtractBetweenText(GetHtml, "<option value=\"\"></option>", "</select>").Split(new string[] { "<option" }, StringSplitOptions.None);

            lvSchool.Items.Clear();

            for (int i = 1; i < univs.Length; i++)
            {
                string num = ExtractBetweenText(univs[i], "value=\"", "\"");
                string name = ExtractBetweenText(univs[i], ">", "<");

                lvSchool.Items.Add(new ListViewItem(new string[] { num, name }));
            }

        }

        private string[] CrawlResumeList(int index)
        {
            string GetHtml = getHTMLFromWeb("https://apply.likelion.org/users/edit");
            string authToken = ExtractBetweenText(GetHtml, "name=\"authenticity_token\" value=\"", "\"");

            string number = lvSchool.Items[index].Text;

            string data = "utf8=%E2%9C%93&_method=put&authenticity_token=" + HttpUtility.UrlEncode(authToken) + "&" + HttpUtility.UrlEncode("user[email]") + "=" + HttpUtility.UrlEncode(EMAIL) + "&" + HttpUtility.UrlEncode("user[university_id]") + "=" + number + "&" + HttpUtility.UrlEncode("user[current_password]") + "=qjrm11&commit=" + HttpUtility.UrlEncode("수정");

            var response = postData("https://apply.likelion.org/users", data);

            GetHtml = getHTMLFromWeb("https://apply.likelion.org/application/new");

            string enrollContent = ExtractBetweenText(GetHtml, "<p class=\"enroll__text\">", "</p>");

            string[] items = GetHtml.Split(new string[] { "<h5 class=\"enroll__required\"" }, StringSplitOptions.None);

            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(enrollContent);
            enrollContent = htmlDoc.DocumentNode.InnerHtml.Replace("<br>", "\n");

            string[] results = new string[7];
            
            results[0] = lvSchool.Items[index].SubItems[1].Text;
            results[1] = enrollContent;

            for (int i = 0; i < items.Length - 6; i++)
            {
                string item = ExtractBetweenText(items[6 + i], ">", "</h5>");
                htmlDoc.LoadHtml(item);

               results[2 + i] = htmlDoc.DocumentNode.InnerHtml.Replace("<br>", "\n");
            }

            return results;
        }

        private void btnTrue_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lvSchool.Items.Count; i++) lvSchool.Items[i].Checked = true;
        }

        private void btnFalse_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lvSchool.Items.Count; i++) lvSchool.Items[i].Checked = false;
        }

        private void btnCrawl_Click(object sender, EventArgs e)
        {
            int checkedCount = 0;

            // 체크박스 갯수 파악

            for (int i = 0; i< lvSchool.Items.Count; i++)
            {
                if (lvSchool.Items[i].Checked == true)
                    checkedCount++;
            }           
            
            // 크롤링 시작

            Excel.Application excelApp = null;
            Excel.Workbook wb = null;
            Excel.Worksheet ws = null;

            excelApp = new Excel.Application();
            wb = excelApp.Workbooks.Add();
            ws = wb.Worksheets.Item[1] as Excel.Worksheet;

            // 헤더

            int r = 1;
            string[] headers = new string[] { "학교 이름", "모집소개", "항목1", "항목2", "항목3", "항목4", "항목5" };
            string[] results = null;

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[r, 1 + i] = headers[i];
            }

            // 크롤링
            for (int i = 0; i < lvSchool.Items.Count; i++)
            {
                if (lvSchool.Items[i].Checked == true)
                {
                    results = CrawlResumeList(i);

                    r++;

                    for (int j = 0; j < results.Length; j++)
                    {
                        ws.Cells[r, 1 + j] = HttpUtility.HtmlDecode(results[j]);
                    }
                }
            }

            wb.SaveAs(SAVEPATH, Excel.XlFileFormat.xlWorkbookNormal);
            wb.Close(true);
            excelApp.Quit();
           
            ReleaseExcelObject(ws);
            ReleaseExcelObject(wb);
            ReleaseExcelObject(excelApp);
            
        }

        private static void ReleaseExcelObject(object obj)
        {
            try
            {
                if (obj != null)
                {
                    Marshal.ReleaseComObject(obj);
                    obj = null;
                }
            }
            catch (Exception ex)
            {
                obj = null;
                throw ex;
            }
            finally
            {
                GC.Collect();
            }
        }

        private string ExtractBetweenText(string Text, string Begin, string End)
        {
            return Text.Split(new string[] { Begin }, StringSplitOptions.None)[1].Split(new string[] { End }, StringSplitOptions.None)[0];
        }

        private string getHTMLFromWeb(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = cookieContainer;
            request.KeepAlive = true;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/48.0.2564.82 Safari/537.36";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Encoding encode = Encoding.GetEncoding("utf-8");
            Stream strReceive = response.GetResponseStream();
            StreamReader reqStreamReader = new StreamReader(strReceive, encode);
            string strResult = reqStreamReader.ReadToEnd();

            return strResult;
        }

        private string postData(string url, string data)
        {
            Byte[] pd = Encoding.UTF8.GetBytes(data);

            HttpWebRequest  request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Credentials = CredentialCache.DefaultCredentials;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36";
            request.ContentType = "application/x-www-form-urlencoded";
            request.KeepAlive = true;
            request.ContentLength = pd.Length;

            if (url.Contains("https://apply.likelion.org/users"))
            {
                request.Referer = "https://apply.likelion.org/users/edit";
            }

            request.CookieContainer = cookieContainer;

            Stream sw = request.GetRequestStream();
            sw.Write(pd, 0, pd.Length);
            sw.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Encoding encode = Encoding.GetEncoding("utf-8");
                Stream strReceive = response.GetResponseStream();
                StreamReader reqStreamReader = new StreamReader(strReceive, encode);
                string strResult = reqStreamReader.ReadToEnd();
                request.Abort();
                strReceive.Close();
                reqStreamReader.Close();

                return strResult;
            }

            return "";
        }
    }
}
