namespace HttpClient;

using System;
using System.IO;
using System.Net;
using System.Text;

public class HttpHelper
{
    public class Props
    {
        public string? url { get; init; }
    }

    private readonly Props _props;

    public HttpHelper(Props props)
    {
        _props = props;
    }


    public String HttpCall(String p_sUrl, String p_sParam, String p_sMethod)
    {
        try
        {
            HttpWebRequest? httpWebRequest = null;
            // 인코딩 UTF-8
            byte[] sendData = UTF8Encoding.UTF8.GetBytes(p_sParam);


            if (p_sMethod == "POST")
            {

                httpWebRequest = (HttpWebRequest)WebRequest.Create(p_sUrl);
                httpWebRequest.ContentType = "application/json; charset=UTF-8";
                httpWebRequest.Method = p_sMethod;
                httpWebRequest.ContentLength = sendData.Length;

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(p_sParam);
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                // Stream requestStream = httpWebRequest.GetRequestStream();
                // requestStream.Write(p_sParam, 0, sendData.Length);
                // requestStream.Close();
            }
            else if (p_sMethod == "GET")
            {
                httpWebRequest = (HttpWebRequest)WebRequest.Create(_props.url + "?" + p_sParam);
                httpWebRequest.Method = p_sMethod;
            }

            HttpWebResponse httpWebResponse;
            using (httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                //status code
                Console.WriteLine($"status code: {httpWebResponse.StatusCode}");
                StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.GetEncoding("UTF-8"));
                string result = streamReader.ReadToEnd();
                return result;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("error: " + e);
            return String.Empty;
        }
    }
}
