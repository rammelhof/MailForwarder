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
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("MailForwarder.ProcessMails running at: {time}", DateTimeOffset.Now);
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

            // The Inbox folder is always available on all IMAP servers...
            // var ns = new FolderNamespace(';',"/");


            // var folders = client.GetFolders(ns);
            var inbox = imapClient.Inbox;
            inbox.Open(FolderAccess.ReadOnly);

            Console.WriteLine("Total messages: {0}", inbox.Count);

            for (int i = 0; i < inbox.Count; i++)
            {
                var message = inbox.GetMessage(i);
                Console.WriteLine($"Sender: {message.From} Recipient: {message.To} Subject: {message.Subject}");

                var origMessageTo = message.To.Cast<MailboxAddress>().FirstOrDefault(a => (_configuration.MailTo ?? String.Empty).Equals(a.Address));
                if (origMessageTo != null)
                {
                    var toAddress = new MailboxAddress(_configuration.FowardTo ?? String.Empty, _configuration.FowardTo ?? String.Empty);
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
                            string fromSRSAddress = srs.BuildSRSAddress(origMessageFrom.Domain, origMessageFrom.LocalPart, origMessageTo.Domain);
                            var fromAddress = new MailboxAddress(origMessageFrom.Name, fromSRSAddress);

                            message.From.Clear();
                            message.From.Add(fromAddress);

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

                                Console.WriteLine($"=> Sender: {message.From} Recipient: {message.To} Subject: {message.Subject}");
                                smtpClient.Send(message);
                                smtpClient.Disconnect(true);

                                var sentFolder = imapClient.GetFolder(SpecialFolder.Sent);
                                sentFolder.Append(message);
                            }
                        }
                    }
                }

            }

            imapClient.Disconnect(true);
        }


        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("MailForwarder.ProcessMails finished at: {time}", DateTimeOffset.Now);
        }
    }
}
