using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private const string validFormat = "4.0";
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;

        [SetUp]
        public void SetUp()
        {
            file = new File("someFile", new byte[] { 1, 2, 3 });
            signedContent = new byte[] { 1, 7 };

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);
            SetDocumentWith(DateTime.Now);
            A.CallTo(() => cryptographer.Sign(null, null))
                .WithAnyArguments().Returns(signedContent);
            A.CallTo(() => sender.TrySend(null))
                .WithAnyArguments().Returns(true);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            SetDocumentWith(DateTime.Now, format: format);
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [TestCase("abracadabra")]
        [TestCase("3.0")]
        public void Skip_WhenBadFormat(string format)
        {
            SetDocumentWith(DateTime.Now, format: format);
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().HaveCount(1).And.BeEquivalentTo(file);
        }

        [Test]
        public void Skip_WhenOlderThanAMonth()
        {
            SetDocumentWith(DateTime.Now.AddMonths(-1));
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().HaveCount(1).And.BeEquivalentTo(file);
        }

        [TestCase(0)]
        [TestCase(3600)]
        [TestCase(86400)]
        [TestCase(2591999)]
        public void Send_WhenYoungerThanAMonth(int addedSeconds)
        {
            SetDocumentWith(DateTime.Now + new TimeSpan(addedSeconds));
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(false);
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().HaveCount(1).And.BeEquivalentTo(file);
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            SetDocumentWith(DateTime.Now, isRecognized: false);
            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().HaveCount(1).And.BeEquivalentTo(file);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            SetDocumentWith(DateTime.Now, format: "4.0", times: 1);
            SetDocumentWith(DateTime.Now.AddMonths(-1), times: 1);
            SetDocumentWith(DateTime.Now, format: "3.0", times: 1);

            fileSender.SendFiles(new[] { file, file, file }, certificate)
                .SkippedFiles.Should().HaveCount(2);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            A.CallTo(() => sender.TrySend(null))
                .WithAnyArguments().ReturnsNextFromSequence(true, false, true);
            fileSender.SendFiles(new[] { file, file, file }, certificate)
                .SkippedFiles.Should().HaveCount(1);
        }

        private void SetDocumentWith(DateTime time, string format = validFormat, bool isRecognized = true,
            int times = int.MaxValue)
        {
            var document = new Document(file.Name, file.Content, time, format);
            A.CallTo(() => recognizer.TryRecognize(null, out document))
                .WithAnyArguments()
                .Returns(isRecognized).NumberOfTimes(times);
        }
    }
}
