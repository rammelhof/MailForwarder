namespace MailForwarder.Lib;

public class MailForwarderConfiguration
{
    public String? ImapServer { get; set; }
    public String? ImapUser { get; set; }
    public String? ImapPassword { get; set; }
    public int? ImapPort { get; set; }
    public String? SmtpServer { get; set; }
    public String? SmtpUser { get; set; }
    public String? SmtpPassword { get; set; }
    public int? SmtpPort { get; set; }
    public String? MailTo { get; set; }
    public String? FowardTo { get; set; }
    public String? SRSHashKey { get; set; }
}