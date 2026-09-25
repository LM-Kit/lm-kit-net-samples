using LMKit.Document.Conversion;
using LMKit.Document.Pdf;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace pdf_digital_signatures
{
    /// <summary>
    /// PDF digital signatures end to end: sign a document (PAdES), verify it on the
    /// independent axes LM-Kit reports (integrity, identity, revocation, timestamp),
    /// countersign it without breaking the first signature, and watch tampering get
    /// caught. Optionally prove the signing time through an RFC 3161 authority.
    /// </summary>
    internal class Program
    {
        private const string SampleContract = """
            # Service Agreement

            This agreement is entered into between **Contoso Ltd.** and **Fabrikam Inc.**

            ## Terms

            1. The service starts on the first day of the coming month.
            2. Either party may terminate with thirty days written notice.
            3. The total monthly fee is 4,200 EUR.

            Signed electronically with LM-Kit.
            """;

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Set your license key here. A trial runs without one.
            //LMKit.Licensing.LicenseManager.SetLicenseKey("");

            string outputDirectory = Path.Combine(Environment.CurrentDirectory, "output");
            Directory.CreateDirectory(outputDirectory);

            Uri? timestampAuthority = ReadTimestampAuthority(args);

            // 1. The document to sign: the first argument, or a sample contract.
            string inputPath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                ?? Path.Combine(outputDirectory, "contract.pdf");

            if (!File.Exists(inputPath))
            {
                MarkdownToPdf.ConvertToFile(SampleContract, inputPath);
                WriteStep($"Sample contract created: {inputPath}");
            }

            // 2. The signer: a self-signed certificate generated for the demo. In
            //    production, load a PFX or pick a certificate from the OS store; any
            //    private key the BCL reaches works, hardware-backed keys included.
            using X509Certificate2 signer = CreateDemoCertificate("CN=LM-Kit Demo Signer");
            WriteStep($"Demo signer created: {signer.Subject} (self-signed)");

            // Trust is explicit: validation trusts exactly the anchors you configure.
            var trust = new PdfSignatureValidationOptions { TrustSystemRoots = false };
            trust.TrustedRoots.Add(signer);

            // 3. Sign. Signing appends an incremental revision, so it never rewrites
            //    the document it signs. Bounds makes the signature VISIBLE: the field
            //    renders who signed, when, and why (localized via AppearanceCulture,
            //    current UI culture by default) inside the signed bytes themselves.
            string signedPath = Path.Combine(outputDirectory, "contract-signed.pdf");

            var signingOptions = new PdfSigningOptions
            {
                Certificate = signer,
                Reason = "Approved for release",
                Bounds = new PdfSigningOptions.FieldBounds(320, 60, 550, 130),
                TimestampAuthority = timestampAuthority,
            };

            PdfSigner.Sign(inputPath, signedPath, signingOptions);
            WriteStep($"Signed with a visible field: {signedPath}" + (timestampAuthority != null ? " (with an RFC 3161 timestamp)" : ""));

            // 4. Verify: every axis is reported separately.
            PrintReport("After signing", PdfSignatureValidator.Validate(signedPath, trust));

            // 5. Countersign: a second signature in its own revision. The first
            //    signature keeps verifying because its bytes are untouched.
            string countersignedPath = Path.Combine(outputDirectory, "contract-countersigned.pdf");

            PdfSigner.Sign(signedPath, countersignedPath, new PdfSigningOptions
            {
                Certificate = signer,
                Reason = "Countersigned by operations",
                FieldName = "Signature2",
            });

            PrintReport("After countersigning", PdfSignatureValidator.Validate(countersignedPath, trust));

            // 6. Tamper with one byte of signed content and watch validation catch it.
            byte[] tampered = File.ReadAllBytes(signedPath);
            int index = IndexOf(tampered, Encoding.ASCII.GetBytes("Approved for release"));

            if (index > 0)
            {
                tampered[index] = (byte)'X';
                string tamperedPath = Path.Combine(outputDirectory, "contract-tampered.pdf");
                File.WriteAllBytes(tamperedPath, tampered);

                PrintReport("After tampering with one byte", PdfSignatureValidator.Validate(tamperedPath, trust));
            }

            Console.WriteLine();
            Console.WriteLine("Files are in ./output. Open them in any PDF viewer to inspect the signatures.");
            Console.WriteLine("Add --tsa <url> to prove the signing time through an RFC 3161 authority.");
        }

        private static X509Certificate2 CreateDemoCertificate(string subject)
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));

            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        }

        private static void PrintReport(string title, PdfSignatureValidationReport report)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"== {title} ==");
            Console.ResetColor();

            Console.WriteLine($"Overall: {Describe(report.OverallStatus)}");

            foreach (PdfSignatureValidationResult result in report.Signatures)
            {
                Console.WriteLine();
                Console.WriteLine($"Signature #{result.Signature.Index + 1} ({result.Signature.SubFilter})");
                Console.WriteLine($"  Status     : {Describe(result.Status)}");
                Console.WriteLine($"  Integrity  : {result.Integrity}");
                Console.WriteLine($"  Identity   : {result.Identity} ({result.SignerCertificate?.Subject ?? "no certificate"})");
                Console.WriteLine($"  Revocation : {result.Revocation}");
                Console.WriteLine($"  Timestamp  : {result.Timestamp}" + (result.TimestampTime.HasValue ? $" at {result.TimestampTime:u}" : ""));
                Console.WriteLine($"  Covers all : {result.Signature.CoversEntireDocument}");
                Console.WriteLine($"  Detail     : {result.StatusMessage}");
            }
        }

        private static string Describe(PdfSignatureStatus status)
        {
            return status switch
            {
                PdfSignatureStatus.Valid => "VALID",
                PdfSignatureStatus.Invalid => "INVALID",
                _ => "INDETERMINATE",
            };
        }

        private static void WriteStep(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static Uri? ReadTimestampAuthority(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--tsa", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(args[i + 1]);
                }
            }

            return null;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;

                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
