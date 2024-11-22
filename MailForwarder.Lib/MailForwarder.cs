using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailForwarder.Lib;

public class MailForwarder
{
    private IServiceProvider _serviceProvider;
    private readonly ILogger<MailForwarder> _logger;
    private readonly MailForwarderConfiguration _configuration;

    public MailForwarder(IServiceProvider serviceProvider, ILogger<MailForwarder> logger, IOptions<MailForwarderConfiguration> configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public void ProcessMails()
    {
        if (String.IsNullOrEmpty(_configuration.MailTo))
        {
            _logger.LogWarning($"Configuration missing: MailTo");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("MailForwarder.ProcessMails running at: {time}", DateTimeOffset.Now);
            _logger.LogDebug($"ImapServer: {_configuration.ImapServer}");
        }

        using (var imapClient = new ImapClient())
        {
            imapClient.ServerCertificateValidationCallback = (
                                object sender,
                                X509Certificate? certificate,
                                X509Chain? chain,
                                SslPolicyErrors sslPolicyErrors) =>
                            {
                                return true;
                            };

            imapClient.Connect(_configuration.ImapServer, _configuration.ImapPort ?? 993, MailKit.Security.SecureSocketOptions.Auto);
            imapClient.Authenticate(_configuration.ImapUser, _configuration.ImapPassword);

            DebugImapInfo(imapClient);

            var inbox = imapClient.Inbox;
            inbox.Open(FolderAccess.ReadWrite);

            _logger.LogDebug("Total messages: {0}", inbox.Count);

            var query = MailKit.Search.SearchQuery.ToContains(_configuration.MailTo)
                            .Or(MailKit.Search.SearchQuery.ToContains(_configuration.SRSSearchTerm ?? "+SRS="));

            var uids = inbox.Search(query);

            foreach (var messageId in uids)
            {
                var message = inbox.GetMessage(messageId);

                // check for To address match
                var origMessageTo = message.To.Cast<MailboxAddress>().FirstOrDefault(a => (_configuration.MailTo ?? String.Empty).Equals(a.Address));
                if (origMessageTo != null)
                {
                    ForwardMessage(imapClient, inbox, messageId, message);
                }


                // check for SRS pattern match
                var srsMessageTo = message.To.Cast<MailboxAddress>().FirstOrDefault(a => a.Address.Contains(_configuration.SRSSearchTerm ?? "+SRS="));
                if (srsMessageTo != null)
                {
                    SendBackMessage(imapClient, inbox, messageId, message);
                }

            }

            imapClient.Disconnect(true);
        }


        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("MailForwarder.ProcessMails finished at: {time}", DateTimeOffset.Now);
        }
    }
    private void SendBackMessage(ImapClient imapClient, IMailFolder inbox, UniqueId messageId, MimeMessage message)
    {
        _logger.LogInformation($"SendBackMessage: Sender: {message.From} Recipient: {message.To} Subject: {message.Subject}");

        var srsMessageTo = message.To.Cast<MailboxAddress>().FirstOrDefault(a => a.Address.Contains(_configuration.SRSSearchTerm ?? "+SRS="));
        if (srsMessageTo != null)
        {
            var srs = _serviceProvider.GetService(typeof(SRS)) as SRS;
            if (srs != null)
            {
                // parse SRS parts end check hash
                if (srs.CheckSRSAddress(srsMessageTo.Address))
                {
                    string domain = srs.GetSRSAddressOriginalDomain(srsMessageTo.Address);
                    string localPart = srs.GetSRSAddressOriginalLocalPart(srsMessageTo.Address);
                    string address = $"{localPart}@{domain}";
                    var newToAddress = new MailboxAddress(srsMessageTo.Name, address);

                    message.To.Clear();
                    message.To.Add(newToAddress);

                    
                    var newFromAddress = new MailboxAddress(_configuration.MailToName ?? String.Empty, _configuration.MailTo);
                    message.From.Clear();
                    message.From.Add(newFromAddress);

                    message.ReplyTo.Clear();
                    message.ReplyTo.Add(newFromAddress);

                    message.Cc.Clear();
                    message.Bcc.Clear();
                    message.Sender = null;

                    SendMessage(imapClient, inbox, messageId, message);
                }
            }
        }
    }

    private void ForwardMessage(ImapClient imapClient, IMailFolder inbox, UniqueId messageId, MimeMessage message)
    {
        _logger.LogInformation($"ForwardMessage: Sender: {message.From} Recipient: {message.To} Subject: {message.Subject}");

        var origMessageTo = message.To.Cast<MailboxAddress>().FirstOrDefault(a => (_configuration.MailTo ?? String.Empty).Equals(a.Address));
        if (origMessageTo != null)
        {
            var toAddress = new MailboxAddress(_configuration.FowardToName ?? String.Empty, _configuration.FowardTo ?? String.Empty);
            message.To.Clear();
            message.To.Add(toAddress);

            // SRS sender
            var origMessageFrom = message.From.First() as MailboxAddress;
            if (origMessageFrom != null)
            {
                var srs = _serviceProvider.GetService(typeof(SRS)) as SRS;
                if (srs != null)
                {
                    // $"SRS0=HHH=TT={origMessageFrom.Domain}={origMessageFrom.LocalPart}@{origMessageTo.Domain}";
                    string fromSRSAddress = srs.BuildSRSAddress(origMessageFrom.Domain, origMessageFrom.LocalPart, origMessageTo.Domain, origMessageTo.LocalPart);
                    var fromAddress = new MailboxAddress(origMessageFrom.Name, fromSRSAddress);

                    message.From.Clear();
                    message.From.Add(fromAddress);
                    
                    message.ReplyTo.Clear();
                    message.ReplyTo.Add(origMessageFrom);

                    message.Cc.Clear();
                    message.Bcc.Clear();
                    message.Sender = null;

                    SendMessage(imapClient, inbox, messageId, message);
                }
            }
        }
    }

    private void SendMessage(ImapClient imapClient, IMailFolder inbox, UniqueId messageId, MimeMessage message)
    {
        // mail send to
        using (var smtpClient = new SmtpClient())
        {
            // public delegate bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors);
            smtpClient.ServerCertificateValidationCallback = (
                object sender,
                X509Certificate? certificate,
                X509Chain? chain,
                SslPolicyErrors sslPolicyErrors) =>
            {
                return true;
            };


            smtpClient.Connect(_configuration.SmtpServer
                , _configuration.SmtpPort ?? 587
                , MailKit.Security.SecureSocketOptions.Auto
                );

            if (!String.IsNullOrEmpty(_configuration.SmtpUser))
            {
                smtpClient.Authenticate(_configuration.SmtpUser, _configuration.SmtpPassword);
            }

            _logger.LogInformation($"=> Sender: {message.From} Recipient: {message.To} Subject: {message.Subject}");

            smtpClient.Send(message);
            smtpClient.Disconnect(true);

            if (!String.IsNullOrEmpty(_configuration.ImapSentFolder))
            {
                var sentFolder = imapClient.GetFolder(_configuration.ImapSentFolder);
                if (sentFolder != null)
                {
                    sentFolder.Open(FolderAccess.ReadWrite);
                    sentFolder.Append(message);
                }
            }

            if (!String.IsNullOrEmpty(_configuration.ImapArchivFolder))
            {
                if (imapClient.Capabilities.HasFlag(ImapCapabilities.Move))
                {
                    var archivFolder = imapClient.GetFolder(_configuration.ImapArchivFolder);
                    if (archivFolder != null)
                    {
                        if (!inbox.IsOpen)
                            inbox.Open(FolderAccess.ReadWrite);

                        inbox.MoveTo(messageId, archivFolder);
                    }
                }
            }
        }

    }

    private void DebugImapInfo(ImapClient imapClient)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var ns in imapClient.PersonalNamespaces)
                _logger.LogDebug($"PersonalNamespace: \"{ns.Path}\" \"{ns.DirectorySeparator}\"");
            foreach (var ns in imapClient.SharedNamespaces)
                _logger.LogDebug($"SharedNamespaces: \"{ns.Path}\" \"{ns.DirectorySeparator}\"");
            foreach (var ns in imapClient.OtherNamespaces)
                _logger.LogDebug($"OtherNamespaces: \"{ns.Path}\" \"{ns.DirectorySeparator}\"");

            if (_configuration.ImapNamespace != null)
            {
                var ns = imapClient.PersonalNamespaces.FirstOrDefault(n => n.Path.Equals(_configuration.ImapNamespace));
                if (ns == null)
                    ns = imapClient.SharedNamespaces.FirstOrDefault(n => n.Path.Equals(_configuration.ImapNamespace));
                if (ns == null)
                    ns = imapClient.OtherNamespaces.FirstOrDefault(n => n.Path.Equals(_configuration.ImapNamespace));

                if (ns != null)
                {
                    var namespaceFolder = imapClient.GetFolder(ns);
                    var subfolders = namespaceFolder.GetSubfolders();
                    foreach (var folder in subfolders)
                        _logger.LogDebug($"{folder.FullName}");
                }
            }
        }
    }
}
