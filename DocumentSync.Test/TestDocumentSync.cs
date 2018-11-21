using System;
using NUnit.Framework;

namespace DocumentSync.Test
{
    [TestFixture()]

    public class TestDocumentSync
    {
        public TestDocumentSync()
        {
        }


        [SetUp]
        protected void SetUp()
        { }

        [Test]
        public void Test_SyncAllEqual()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void Test_SyncNewer() => throw new NotImplementedException();

        [Test]
        public void Test_SyncMissing() => throw new NotImplementedException();

        public void Test_FastDocumentComparer => throw new NotImplementedException();
        public void TesT_FullDocumentComparer => throw new NotImplementedException();
    }
}