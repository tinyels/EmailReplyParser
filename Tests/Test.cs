using System.Security.Cryptography;
using EmailReplyParser;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

// ReSharper disable UnusedParameter.Local

namespace EmailReplyParserTests
{
	public class Test
	{
		private Email _reply;
		private IList<Fragment> _rf;

		[Test]
		public void ReadsSimpleBody()
		{
			GetEmailByName("email_1_1");
			Assert.AreEqual(3, _rf.Count);
			Assert.IsFalse(_rf.Any(f => f.IsQuoted));
			Assert.AreEqual(new[] { false, true, true },
				_rf.Select(f => f.IsSignature));
			Assert.AreEqual(new[] { false, true, true },
				_rf.Select(f => f.IsHidden));
			const string expected = "Hi folks\n\nWhat is the best way to clear a Riak bucket of all key, values after\nrunning a test?\nI am currently using the Java HTTP API.\n";
			Assert.AreEqual(expected, _rf[0].Content);
			Assert.AreEqual("-Abhishek Kona\n\n", _rf[1].Content);
		}

		[Test]
		public void ReadsTopPost()
		{
			GetEmailByName("email_1_3");
			Assert.AreEqual(5, _rf.Count);
			Assert.AreEqual(new[] { false, false, true, false, false },
				_rf.Select(f => f.IsQuoted));
			Assert.AreEqual(new[] { false, true, true, true, true },
			  _rf.Select(f => f.IsHidden));

			Assert.AreEqual(new[] { false, true, false, false, true },
			  _rf.Select(f => f.IsSignature));

			AssertMatch("^Oh thanks.\n\nHaving", _rf[0].Content);
			AssertMatch("^-A", _rf[1].Content);
			AssertMatch(@"^On [^\:]+\:", _rf[2].Content);
			AssertMatch("^_", _rf[4].Content);
		}

		[Test]
		public void ReadsBottomPost()
		{
			GetEmailByName("email_1_2");
			Assert.AreEqual(6, _rf.Count);

			Assert.AreEqual(new[] { false, true, false, true, false, false },
				_rf.Select(f => f.IsQuoted));
			Assert.AreEqual(new[] { false, false, false, false, false, true },
			  _rf.Select(f => f.IsSignature));
			Assert.AreEqual(new[] { false, false, false, true, true, true },
			  _rf.Select(f => f.IsHidden));
			AssertMatch("^Hi,$", _rf[0].Content);
			AssertMatch("On [^\\:]+\\:", _rf[1].Content);
			AssertMatch("^You can list", _rf[2].Content);
			AssertMatch("^> ", _rf[3].Content);
			AssertMatch("^_", _rf[5].Content);
		}

		[Test]
		public void RecognizesDateStringAboveQuote()
		{
			GetEmailByName("email_1_4");
			AssertMatch("^Awesome", _rf[0].Content);
			AssertMatch("^On", _rf[1].Content);
			AssertMatch("Loader", _rf[1].Content);
		}

		[Test]
		public void ComplexBodyWithOnlyOneFragment()
		{
			GetEmailByName("email_1_5");
			Assert.AreEqual(1, _rf.Count);
		}

		[Test]
		public void ReadsEmailWithCorrectSignatures()
		{
			GetEmailByName("correct_sig");
			Assert.AreEqual(new[] { false, false },
			_rf.Select(f => f.IsQuoted));
			Assert.AreEqual(new[] { false, true },
			  _rf.Select(f => f.IsSignature));
			Assert.AreEqual(new[] { false, true },
			  _rf.Select(f => f.IsHidden));
			AssertMatch("^-- \nrick", _rf[1].Content);
		}

		//[Test]
		public void DealsWithMultilineReplyHeaders()
		{
			GetEmailByName("email_1_6");
			AssertMatch("^I get", _rf[0].Content);
			AssertMatch("^On", _rf[1].Content);
			AssertMatch("Was This", _rf[1].Content);
		}

		//[Test]
		public void DealsWithWindowsLineEndings()
		{
			GetEmailByName("email_1_7");
			AssertMatch(":\\+1:", _rf[0].Content);
			AssertMatch("^On", _rf[1].Content);
			AssertMatch("Steps 0-2", _rf[1].Content);
		}

		[Test]
		public void ReturnsOnlyTheVisibleFragmentsAsAString()
		{
			GetEmailByName("email_2_1");
			Assert.AreEqual(string.Join("\n", _rf.Where(f => !f.IsHidden).Select(f => f.Content)).TrimEnd(), _reply.VisibleText);
		}

		[Test]
		public void ParseOutJustTopForOutlookReply()
		{
			GetEmailByName("email_2_1");
			Assert.AreEqual("Outlook with a reply", _reply.VisibleText);
		}

		[Test]
		public void ParseOutJustTopForOutlookReplyDirectlyAboveLine()
		{
			GetEmailByName("email_2_2");
			Assert.AreEqual("Outlook with a reply directly above line", _reply.VisibleText);
		}

		[Test]
		public void ParseOutSentFromIPhone()
		{
			GetEmailByName("email_iPhone");
			Assert.AreEqual("Here is another email", _reply.VisibleText);
		}

		[Test]
		public void ParseOutSentFromBlackberry()
		{
			GetEmailByName("email_BlackBerry");
			Assert.AreEqual("Here is another email", _reply.VisibleText);
		}

		[Test]
		public void ParseOutSentFromMultiwordMobileDevice()
		{
			GetEmailByName("email_multi_word_sent_from_my_mobile_device");
			Assert.AreEqual("Here is another email", _reply.VisibleText);
		}

		[Test]
		public void DoNotParseOutSendFromRegularSentence()
		{
			GetEmailByName("email_sent_from_my_not_signature");
			Assert.AreEqual("Here is another email\n\nSent from my desk, is much easier then my mobile phone.", _reply.VisibleText);
		}

		[Test]
		public void RetainBullets()
		{
			GetEmailByName("email_bullets");
			Assert.AreEqual("test 2 this should list second\n\nand have spaces\n\nand retain this formatting\n\n\n   - how about bullets\n   - and another", _reply.VisibleText);
		}

		//[Test]
		public void ParseReply()
		{
			var text = GetFileContents("email_1_2");
			var body = Regex.Replace(text, "\r\n", "\n");
			GetEmailByName("email_1_2");
			Assert.AreEqual(body, _reply.VisibleText);
		}

		[Test]
		public void OneIsNotOn()
		{
			GetEmailByName("email_one_is_not_on");
			AssertMatch("One outstanding question", _rf[0].Content);
			AssertMatch("^On Oct 1, 2012", _rf[1].Content);
		}

		private void GetEmailByName(string name)
		{
			_reply = new Email(GetFileContents(name));
			_rf = _reply.Fragments;
		}

		private void AssertMatch(string expectedRegex, string actual)
		{
			Assert.IsTrue(Regex.IsMatch(actual, expectedRegex, RegexOptions.Multiline));
		}

		private string GetFileContents(string sampleFile)
		{
			var asm = Assembly.GetExecutingAssembly();
			var resource = string.Format("EmailReplyParserTests.Emails.{0}.txt",
				sampleFile);
			using (var stream = asm.GetManifestResourceStream(resource))
			{
				if (stream != null)
				{
					var reader = new StreamReader(stream);
					return reader.ReadToEnd();
				}
			}
			return string.Empty;
		}
	}
}

// ReSharper restore UnusedParameter.Local