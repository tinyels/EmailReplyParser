using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmailReplyParser
{
	//An Email instance represents a parsed body String.
	public class Email
	{
		private readonly Regex _quoteHeaderRegex = new Regex("^:etorw.*nO$");
		private readonly Regex _quoteRegex = new Regex("(>+)$");
		private readonly Regex _sigRegex = new Regex(@"(?m)(--|__|\w-$)|(^(\w+\s*){1,3} ym morf tneS$)");
		private Fragment _currentFragment;
		private IList<Fragment> _fragments = new List<Fragment>();
		private bool _foundVisible;

		public Email(string text)
		{
			Read(text);
		}

		public string VisibleText
		{
			get { return string.Join("\n", _fragments.Where(f => !f.IsHidden).Select(f => f.Content)).TrimEnd(); }
		}

		public IList<Fragment> Fragments
		{
			get { return _fragments; }
		}

		private void Read(string text)
		{
			// normalize line endings
			text = Regex.Replace(text, "\r\n", "\n");

			// todo: Check for multi-line reply headers. Some clients break up
			// the "On DATE, NAME <EMAIL> wrote:" line into multiple lines.
			//if (Regex.IsMatch(text, "^On\\s(.+)wrote:)$", RegexOptions.Multiline))
			//{
			//	Regex.Replace(text,"^On\\s(.+)wrote:)$")
			//}

			// Some users may reply directly above a line of underscores.
			// In order to ensure that these fragments are split correctly,
			// make sure that all lines of underscores are preceded by
			// at least two newline characters.
			text = Regex.Replace(text, "([^\n])(?=\n_{7}_+)$", "${1}\n", RegexOptions.Multiline);

			//reverse text
			text = new string(text.Reverse().ToArray());

			_foundVisible = false;
			_currentFragment = null;
			foreach (var s in text.Split(new[] { "\n" }, StringSplitOptions.None))
			{
				ScanLine(s);
			}
			FinishFragment();
			_fragments = _fragments.Reverse().ToList();
		}

		private void ScanLine(string line)
		{
			line = line.TrimEnd('\n');
			if (!_sigRegex.IsMatch(line))
				line = line.TrimStart();

			var isQuoted = _quoteRegex.IsMatch(line);
			//Mark the current Fragment as a signature if the current line is empty
			// and the Fragment starts with a common signature indicator.
			if (_currentFragment != null && string.IsNullOrEmpty(line))
			{
				if (_sigRegex.IsMatch(_currentFragment.Lines.Last()))
				{
					_currentFragment.IsSignature = true;
					FinishFragment();
				}
			}
			//If the line matches the current fragment, add it.  Note that a common
			//reply header also counts as part of the quoted Fragment, even though
			//it doesn't start with `>`.
			if (_currentFragment != null &&
				((_currentFragment.IsQuoted == isQuoted) ||
				 (_currentFragment.IsQuoted && (QuoteHeader(line) || string.IsNullOrEmpty(line)))))
			{
				_currentFragment.Lines.Add(line);
			}
			else
			{
				FinishFragment();
				_currentFragment = new Fragment(isQuoted, line);
			}
		}

		//Builds the fragment string and reverses it, after all lines have been
		//added.  It also checks to see if this Fragment is hidden.  The hidden
		//Fragment check reads from the bottom to the top.

		//Any quoted Fragments or signature Fragments are marked hidden if they
		//are below any visible Fragments.  Visible Fragments are expected to
		//contain original content by the author.  If they are below a quoted
		//Fragment, then the Fragment should be visible to give context to the
		//reply.

		//	some original text (visible)

		//	> do you have any two's? (quoted, visible)

		//	Go fish! (visible)

		//	> --
		//	> Player 1 (quoted, hidden)

		//	--
		//	Player 2 (signature, hidden)
		private void FinishFragment()
		{
			if (_currentFragment != null)
			{
				//_currentFragment.Finish();
				if (!_foundVisible)
				{
					if (_currentFragment.IsQuoted || _currentFragment.IsSignature ||
						_currentFragment.IsEmpty)
					{
						_currentFragment.IsHidden = true;
					}
					else
					{
						_foundVisible = true;
					}
				}
				_fragments.Add(_currentFragment);
				_currentFragment = null;
			}
		}

		//Detects if a given line is a header above a quoted area.  It is only
		//checked for lines preceding quoted regions.
		//line - A String line of text from the email.
		//Returns true if the line is a valid header, or false.
		private bool QuoteHeader(string line)
		{
			return _quoteHeaderRegex.IsMatch(line);
		}
	}

	public class Fragment
	{
		public IList<string> Lines = new List<string>();

		public Fragment(bool isQuoted, string firstLine)
		{
			Lines.Add(firstLine);
			IsQuoted = isQuoted;
		}

		public bool IsQuoted { get; private set; }

		public bool IsSignature { get; set; }

		public bool IsHidden { get; set; }

		public bool IsEmpty { get { return String.IsNullOrWhiteSpace(string.Concat(Lines)); } }

		public string Content
		{
			get { return new string(string.Join("\n", Lines).Reverse().ToArray()); }
		}
	}
}