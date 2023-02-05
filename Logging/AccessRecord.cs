using System;
using System.Text;
namespace Stargate.Logging
{
	public class AccessRecord
	{
        public string Date { get; set; } = "-";
        public string Time { get; set; } = "-";

        public string RemoteIP { get; set; } = "-";
        public string Url { get; set; } = "-";
        public string StatusCode { get; set; } = "-";
        public string Meta { get; set; } = "-";

        public string SentBytes { get; set; } = "-";
        public string TimeTaken { get; set; } = "-";


        public static string FormatDate(DateTime dt)
            => dt.ToString("yyyy-MM-dd");

        public static string FormatTime(DateTime dt)
            => dt.ToString("HH:mm:ss");

        public static string ComputeTimeTaken(DateTime received, DateTime completed)
            => completed.Subtract(received).Milliseconds.ToString();

        /// <summary>
        /// used to santitizing untrusted input before going in a log field
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string Sanitize(string s, bool allowSpace = true)
        {
            var ret = new StringBuilder(s.Length);
            foreach(char c in s)
            {
                if (c == ' ')
                {
                    if (allowSpace)
                    {
                        ret.Append(c);
                    }
                    else
                    {
                        ret.Append('*');
                    }
                }
                else if (Char.IsAscii(c) &&
                    !Char.IsControl(c) && c != '\"')
                {
                    ret.Append(c);
                }
                else
                {
                    ret.Append('*');
                }
            }
            return ret.ToString();
        }
    }

}

