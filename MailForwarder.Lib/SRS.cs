using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MailForwarder.Lib;


/// <summary>
/// https://github.com/samcday/srs.js/blob/master/srs.js
/// </summary>
public class SRS
{
    private static double timePrecision = (60 * 60 * 24);
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private readonly ILogger<MailForwarder> _logger;
    private readonly MailForwarderConfiguration _configuration;

    public SRS(ILogger<MailForwarder> logger, IOptions<MailForwarderConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration.Value;
    }


    public string BuildSRSAddress(string origSenderDomain, string origSenderLocalPart, string newSenderDomain, string newSenderLocalPart)
    {
        string timestamp = makeTimestamp();
        string hash = makeHash($"{origSenderDomain};{origSenderLocalPart}");
        string fromSRSAddress = (_configuration.SRSTemplate ?? "SRS0={hash}={timestamp}={origSenderDomain}={origSenderLocalPart}@{newSenderDomain}")
            .Replace("{hash}",hash)
            .Replace("{timestamp}", timestamp)
            .Replace("{origSenderDomain}", origSenderDomain)
            .Replace("{origSenderLocalPart}", origSenderLocalPart)
            .Replace("{newSenderDomain}", newSenderDomain)
            .Replace("{newSenderLocalPart}", newSenderLocalPart);
        return fromSRSAddress;
    }

    private string makeHash(string clearText)
    {
        byte[] key = Encoding.Unicode.GetBytes(_configuration.SRSHashKey ?? "R2D2");
        byte[] data = Encoding.Unicode.GetBytes(clearText);
        byte[] hash = HMACSHA1.HashData(key, data);
        var base32String = Encode(hash);
        return base32String.Substring(0, 3);
    }

    public string makeTimestamp()
    {
        var nowUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var now = (int)Math.Round((nowUnixTime / timePrecision) % Math.Pow(2, 10), 0);
        var nowRaw = GetBytes(now, 2);

        var base32String = Encode(nowRaw, 10);
        return base32String;
    }

    public static byte[] GetBytes(int value, int size)
    {
        byte[] buffer = new byte[size];
        for (int i = 0; i < size; i++)
        {
            buffer[i] = (byte)(value >> (i * 8));
        }

        return buffer;
    }


    public static string Encode(byte[] data, int bitsAmount = int.MaxValue)
    {
        StringBuilder result = new StringBuilder();
        int buffer = 0, bitsInBuffer = 0, totalBits = 0;
        foreach (byte b in data)
        {
            buffer |= (b << bitsInBuffer);

            totalBits += 8;
            bitsInBuffer += 8;

            if (totalBits > bitsAmount)
            {
                bitsInBuffer -= (totalBits - bitsAmount);
            }

            while (bitsInBuffer >= 5 && totalBits <= bitsAmount)
            {
                int index = buffer & 0x1F; // Get 5 bits
                result.Insert(0, Base32Alphabet[index]);
                buffer >>= 5;
                bitsInBuffer -= 5;
            }
        }
        // Handle remaining bits
        if (bitsInBuffer > 0)
        {
            buffer <<= (5 - bitsInBuffer);
            result.Insert(0, Base32Alphabet[buffer & 0x1F]);
        }
        return result.ToString();
    }

    public static byte[] Decode(string base32)
    {
        int bitsInBuffer = 0, buffer = 0;
        int byteCnt = (int)Math.Ceiling(base32.Length * 5.0 / 8);
        byte[] result = new byte[byteCnt]; // Max size
        int index = 0;
        for(int i=base32.Length-1;i>=0;i--)
        {
            char c = base32[i];
            if (c == '=' || !Base32Alphabet.Contains(c)) continue; // Skip padding and invalid characters
            buffer |= Base32Alphabet.IndexOf(c) << bitsInBuffer;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                result[index++] = (byte)(buffer); // Get 8 bits
                buffer >>= 8;
                bitsInBuffer -= 8;
            }
        }

        if (bitsInBuffer > 0)
        {
            result[index++] = (byte)(buffer); // Get 8 bits
        }
        
        return result;
    }
}