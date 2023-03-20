using System;

namespace SimpleDnsCrypt.Models
{
	public class IPLogLine : LogLine
	{
		public DateTime Time { get; set; }
		public string Host { get; set; }
		public string QName { get; set; }
		public string IP { get; set; }
		public string Message { get; set; }

		public IPLogLine(string line)
		{
			try
			{
				//this only works with the ltsv log format: 
				//time:1676634103	host:::1	qname:www.generation-nt.com	ip:51.178.73.217	message:51.178.73.217
				var stringSeparators = new[] { "\t" };
				var parts = line.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 5) return;
				if (parts[0].StartsWith("time:"))
				{
					Time = UnixTimeStampToDateTime(Convert.ToDouble(parts[0].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1]));
				}
				if (parts[1].StartsWith("host:"))
				{
					Host = parts[1].Split(new[] { ':' }, 2)[1];
				}
				if (parts[2].StartsWith("qname:"))
				{
					QName = parts[2].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
				}
				if (parts[3].StartsWith("ip:"))
				{
					IP = parts[3].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
				}
				if (parts[4].StartsWith("message:"))
				{
					Message = parts[4].Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
				}
			}
			catch (Exception)
			{
			}
		}
	}
}