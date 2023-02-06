using System;
namespace Stargate.Logging
{
	public class W3CLogger
	{

        TextWriter Logger;
        bool HaveWrittenHeader;

        public W3CLogger(TextWriter logger)
        {
            Logger = logger;
            HaveWrittenHeader = false;
        }

        public void LogAccess(AccessRecord record)
        {
            if(!HaveWrittenHeader)
            {
                HaveWrittenHeader = true;
                WriteHeader();
            }

            Logger.WriteLine($"{record.Date} {record.Time} {record.RemoteIP} {record.Url} {record.StatusCode} \"{record.Meta}\" {record.SentBytes} {record.TimeTaken}");
        }

        private void WriteHeader()
        {
            Logger.WriteLine("#Version: 1.0");
            Logger.WriteLine($"#Date: {DateTime.Now.ToUniversalTime().ToString("dd-MMM-yyyy HH:mm:ss")}");
            Logger.WriteLine("#Fields: date time c-ip cs-uri sc-status x-meta sc-bytes sc-time-taken");
        }
    }
}

