using NUnit.Framework;

namespace Tests
{
    public class GitTests
    {
        [Test]
        public void GetLastCommitTest()
        {
            const string expected = "b672ccf7ab13a4469dd0b477f4f974800da1d661";
            Assert.AreEqual(expected, Git.GetLastCommit());
        }
    }
}
