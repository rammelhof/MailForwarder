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
    public String? ImapNamespace { get; set; }
    public String? ImapSentFolder { get; set; }
    public String? ImapArchivFolder { get; set; }
    public String? SRSTemplate { get; set; }
    public String? PushUrlOk { get; set; }
    public String? PushUrlError { get; set; }
}