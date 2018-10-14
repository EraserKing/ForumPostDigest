using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;

namespace ForumPostDigest
{
    public class WebHandler
    {
        private CookieContainer _cookies = new CookieContainer();
        public HttpClientHandler httpClientHandler;
        public HttpClient httpClient;

        public WebHandler()
        {
            httpClientHandler = new HttpClientHandler();
            httpClient = new HttpClient(httpClientHandler);

            httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", File.ReadAllText("cookie.txt"));
        }
    }

    public class Program
    {
        public static string GetSha256FromString(string strData)
        {
            var message = Encoding.UTF8.GetBytes(strData);
            SHA256Managed hashString = new SHA256Managed();
            string hex = "";

            var hashValue = hashString.ComputeHash(message);
            foreach (byte x in hashValue)
            {
                hex += string.Format("{0:x2}", x);
            }
            return hex;
        }

        static void Main(string[] args)
        {
            bool downloadImage = true;

            string baseUrl = "https://bbs.saraba1st.com/2b/thread-{0}-{1}-1.html";
            string threadNumber = args[0];

            if(!Directory.Exists(threadNumber))
            {
                Directory.CreateDirectory(threadNumber);
            }

            string opAuthor = null;
            string opSubject = null;
            StringBuilder fullHtmlBuilder = new StringBuilder();

            WebHandler handler = new WebHandler();
            fullHtmlBuilder.Append("<html>");
            bool lastPage = false;
            for (int pageNumber = 1, postNumber = 1; !lastPage; pageNumber++)
            {
                Uri url = new Uri(string.Format(baseUrl, threadNumber, pageNumber));
                Console.WriteLine($"Page {pageNumber}");

                var htmlPage = new HtmlDocument();
                htmlPage.OptionEmptyCollection = true;
                htmlPage.Load(handler.httpClient.GetStreamAsync(url).Result);

                if (htmlPage.DocumentNode.SelectNodes("//a[@class='nxt']").Count == 0)
                    lastPage = true;

                // Get OP Author, and only leave posts by this OP
                if (pageNumber == 1)
                {
                    opAuthor = htmlPage.DocumentNode.SelectNodes("//div[@class='authi']/a[@class='xw1']")[0].InnerText;
                    opSubject = htmlPage.DocumentNode.SelectSingleNode("//span[@id='thread_subject']").InnerText;
                    fullHtmlBuilder.Append($"<head><title>{opSubject}</title></head><body>");
                    fullHtmlBuilder.Append($"<a href=\"{url}\">{opSubject}</a><p>");
                }

                foreach (var post in htmlPage.DocumentNode.SelectNodes("//div[@id='postlist']/div[starts-with(@id, 'post_')]"))
                {
                    Console.WriteLine($"  Post {postNumber}");
                    if (post.SelectSingleNode(".//div[@class='authi']/a[@class='xw1']").InnerText != opAuthor)
                    {
                        postNumber++;
                        continue;
                    }

                    HtmlNode postBody = post.SelectSingleNode(".//td[@class='t_f']");

                    // Remove mage tips
                    foreach (var imageTip in postBody.SelectNodes(".//div[contains(@class, 'aimg_tip')]"))
                        imageTip.Remove();

                    int imageNumber = 1;
                    HtmlNodeCollection postImageCollection = postBody.SelectNodes(".//img[starts-with(@id, 'aimg')]");
                    foreach (var image in postImageCollection)
                    {
                        string imageUrl = image.Attributes["file"].Value;
                        try
                        {
                            imageUrl = new Uri(url, image.Attributes["file"].Value).AbsoluteUri;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        string imageName = GetSha256FromString(imageUrl) + Path.GetExtension(imageUrl);

                        if (downloadImage)
                        {
                            Console.WriteLine($"    [{imageNumber++}/{postImageCollection.Count}] {imageUrl} => {imageName}");
                            if (!File.Exists(Path.Combine(threadNumber, imageName)))
                            {
                                FileStream imageFs = new FileStream(Path.Combine(threadNumber, imageName), FileMode.Create);
                                handler.httpClient.GetStreamAsync(imageUrl).Result.CopyTo(imageFs);
                                imageFs.Close();
                            }
                        }
                        if (image.Attributes.Contains("src"))
                        {
                            image.Attributes["src"].Value = imageName;
                        }
                        else
                        {
                            image.Attributes.Add("src", imageName);
                        }
                    }

                    fullHtmlBuilder.Append($"<p>Post {postNumber}<p>");
                    fullHtmlBuilder.Append(post.SelectSingleNode(".//td[@class='t_f']").InnerHtml);
                    fullHtmlBuilder.Append("<p>==========<p>");
                    
                    postNumber++;
                }
                Console.WriteLine($"{DateTime.Now} Get page {pageNumber} Done");
            }
            fullHtmlBuilder.Append("</body></html>");
            FileStream fs = new FileStream(Path.Combine(threadNumber, "Done.html"), FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            sw.Write(fullHtmlBuilder.ToString());
            sw.Close();
            fs.Close();

            Console.WriteLine("All page has been downloaded");
            Console.ReadLine();
        }

        public void SetCookies()
        {

        }
    }
}
