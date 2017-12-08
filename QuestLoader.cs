using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LeopotamGroup.Questbook {
    /// <summary>
    /// Class-helper, provides api for loading / parsing quest markup data.
    /// </summary>
    public static class QuestLoader {
        class QuestStream {
            public int pointer;

            string[] _lines;

            int _linesCount;

            public QuestStream (string markup) {
                _lines = markup.Split (new [] { '\n' });
                _linesCount = _lines.Length;
                pointer = 0;
            }

            public bool Eof () {
                return pointer >= _linesCount;
            }

            public void Previous () {
                if (pointer > 0) { pointer--; }
            }

            public string Next () {
                if (Eof ()) { return string.Empty; }
                var line = _lines[pointer].Trim ();
                pointer++;
                return line.Length > 0 && line.IndexOf ("//") != 0 ? line : Next ();
            }
        }

        enum QuestLogicFilter {
            State,
            Expression
        }

        static readonly Dictionary<string, bool> _linksToValidate = new Dictionary<string, bool> ();

        static readonly StringBuilder _pageTextsBuffer = new StringBuilder ();

        /// <summary>
        /// Loads and parses quest markup data to QuestDocument.
        /// No additional logical structure checks, use "validate" method for it.
        /// </summary>
        /// <param name="markup">Markup source of quest data.</param>
        public static QuestDocument LoadMarkup (string markup) {
            if (string.IsNullOrEmpty (markup)) {
                throw new Exception ("Invalid markup data");
            }
            var stream = new QuestStream (markup);
            var doc = new QuestDocument ();
            while (!stream.Eof ()) {
                var lineId = stream.pointer;
                string name;
                var page = ParsePage (stream, out name);
                if (doc.pages.ContainsKey (name)) {
                    throw new Exception (
                        string.Format ("Error at {0} line: page with name \"{1}\" already declared before", lineId, name));
                }
                doc.pages[name] = page;
                if (string.IsNullOrEmpty (doc.entry)) {
                    doc.entry = name;
                }
            }
            doc.ResetProgress ();
            return doc;
        }

        /// <summary>
        /// Validates QuestDocument for possible errors. Exception will be thrown on any detected error.
        /// </summary>
        /// <param name="doc">Document to validate.</param>
        public static void Validate (QuestDocument doc) {
            _linksToValidate.Clear ();
            _linksToValidate[doc.entry] = true;
            foreach (var pagePair in doc.pages) {
                var page = pagePair.Value;
                if (page == null) {
                    throw new Exception (string.Format ("Invalid page with name \"{0}\"", pagePair.Key));
                }
                if (page.texts == null || page.texts.Count == 0) {
                    throw new Exception (string.Format ("Page \"{0}\": no texts", pagePair.Key));
                }
                if (page.choices == null || page.choices.Count == 0) {
                    throw new Exception (string.Format ("Page \"{0}\": no choices", pagePair.Key));
                }
                for (var i = 0; i < page.choices.Count; i++) {
                    var link = page.choices[i].link;
                    if (link != "end") {
                        if (!doc.pages.ContainsKey (link)) {
                            _linksToValidate.Clear ();
                            throw new Exception (string.Format ("Invalid link from page \"{0}\" to unknown \"{1}\"", pagePair.Key, link));
                        }
                        _linksToValidate[link] = true;
                    }
                }
            }
            foreach (var pagePair in doc.pages) {
                if (!_linksToValidate.ContainsKey (pagePair.Key)) {
                    _linksToValidate.Clear ();
                    throw new Exception (string.Format ("Page without references: \"{0}\"", pagePair.Key));
                }
            }
            _linksToValidate.Clear ();
        }

        static QuestPage ParsePage (QuestStream stream, out string pageName) {
            var line = stream.Next ();
            var matching = Regex.Match (line, "->\\s*?(\\b.+)");
            if (!matching.Success) {
                throw new Exception (string.Format ("Invalid page header at line: {0}", stream.pointer));
            }
            var name = matching.Groups[1].Value.ToLowerInvariant ();
            if (name == "end") {
                throw new Exception (string.Format ("Invalid page name at line: {0}", stream.pointer));
            }
            var page = new QuestPage ();
            ParsePageLogics (page, stream);
            ParsePageTexts (page, stream);
            ParsePageChoices (page, stream);
            pageName = name;
            return page;
        }

        static void ParsePageLogics (QuestPage page, QuestStream stream) {
            while (true) {
                var line = stream.Next ();
                if (line.Length == 0) { break; }
                if (line[0] != '{') { stream.Previous (); break; }
                var matching = Regex.Match (line, "\\{(.+?)\\}");
                if (!matching.Success || string.IsNullOrEmpty (matching.Groups[1].Value)) {
                    throw new Exception (string.Format ("Invalid logic at line: {0}", stream.pointer));
                }
                var cond = ParseLogic (QuestLogicFilter.State, matching.Groups[1].Value, stream.pointer);
                if (cond != null) {
                    if (page.logics == null) { page.logics = new List<QuestLogic> (); }
                    page.logics.Add (cond);
                }
            }
        }

        static void ParsePageTexts (QuestPage page, QuestStream stream) {
            _pageTextsBuffer.Length = 0;
            while (true) {
                var line = stream.Next ();
                if (line.Length == 0) { break; }
                if (line[0] == '*' || line[0] == '-') { stream.Previous (); break; }
                if (_pageTextsBuffer.Length > 0) { _pageTextsBuffer.Append (" "); }
                _pageTextsBuffer.Append (line);
            }
            if (_pageTextsBuffer.Length == 0) {
                throw new Exception (string.Format ("Invalid page texts at line: {0}", stream.pointer));
            }
            page.texts.AddRange (Regex.Split (_pageTextsBuffer.ToString (), "\\s?\\[br\\]\\s?"));
        }

        static void ParsePageChoices (QuestPage page, QuestStream stream) {
            var lineId = stream.pointer;
            while (true) {
                var line = stream.Next ();
                if (line.Length == 0) { break; }
                if (line[0] != '*') { stream.Previous (); break; }
                var matching = Regex.Match (line, "\\*(.*?)->\\s*?(\\b.+)");
                if (!matching.Success) {
                    throw new Exception (string.Format ("Invalid choice syntax at line: {0}", lineId));
                }
                lineId = stream.pointer;
                var choice = new QuestChoice ();
                choice.link = matching.Groups[2].Value.ToLowerInvariant ();
                var rawChoiceText = matching.Groups[1].Value;
                if (!string.IsNullOrEmpty (rawChoiceText)) {
                    var matchingCond = Regex.Match (rawChoiceText, "\\{(.*?)\\}(.+)");
                    if (!matchingCond.Success) {
                        choice.text = rawChoiceText.Trim ();
                    } else {
                        choice.condition = ParseLogic (QuestLogicFilter.Expression, matchingCond.Groups[1].Value, lineId);
                        choice.text = matchingCond.Groups[2].Value.Trim ();
                    }
                }
                page.choices.Add (choice);
            }
            if (page.choices.Count == 0) {
                throw new Exception (string.Format ("No choices at line: {0}", lineId));
            }
            if (page.choices.Count == 1 && page.choices[0].condition != null) {
                throw new Exception (string.Format ("Auto choice cant use condition at line: {0}", lineId));
            }
        }

        static QuestLogic ParseLogic (QuestLogicFilter filter, string code, int lineId) {
            var matchingFull = Regex.Match (code, "(\\w+)\\s*(?:([<>=+!]+)\\s*(-?\\w+)?)?");
            if (!matchingFull.Success ||
                (!string.IsNullOrEmpty (matchingFull.Groups[2].Value) && string.IsNullOrEmpty (matchingFull.Groups[3].Value))) {
                throw new Exception (string.Format ("Invalid logic syntax at line: {0}", lineId));
            }
            var logic = new QuestLogic ();
            logic.lhs = matchingFull.Groups[1].Value.ToLowerInvariant ();
            if (!string.IsNullOrEmpty (matchingFull.Groups[3].Value)) {
                logic.operation = matchingFull.Groups[2].Value;
                if (!int.TryParse (matchingFull.Groups[3].Value, out logic.rhs)) {
                    throw new Exception (string.Format ("Invalid expression at line: {0} , only numbers supported on right side", lineId));
                }
            } else {
                logic.operation = ">";
                logic.rhs = 0;
            }
            switch (logic.operation) {
                case "+=":
                case "=":
                    if (filter != QuestLogicFilter.State) {
                        throw new Exception (string.Format ("Should be conditional expression at line: {0}", lineId));
                    }
                    break;
                case "!=":
                case "==":
                case "<":
                case ">":
                    if (filter != QuestLogicFilter.Expression) {
                        throw new Exception (string.Format ("Should be non conditional expression at line: {0}", lineId));
                    }
                    break;
                default:
                    throw new Exception (string.Format ("Invalid logic operation at line: {0}", lineId));
            }
            return logic;
        }
    }
}