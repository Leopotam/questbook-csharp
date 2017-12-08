using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LeopotamGroup.Questbook {
    /// <summary>
    /// Parsed graph of markup data.
    /// </summary>
    [Serializable]
    public class QuestDocument {
        /// <summary>
        /// Name of page that will be used as entrypoint. Dont use / change it directly, use api instead.
        /// </summary>
        public string entry;

        /// <summary>
        /// Map (<key, page>) of all quest pages. Dont use / change it directly, use api instead.
        /// </summary>
        public Dictionary<string, QuestPage> pages = new Dictionary<string, QuestPage> ();

        /// <summary>
        /// Actual progress state, can be saved / loaded. Dont use / change it directly, use api instead.
        /// </summary>
        public QuestProgress state = new QuestProgress ();

        /// <summary>
        /// Uses specified choice to activate redirection to new page.
        /// </summary>
        /// <param name="id">Choice number (zero-based) at choices list of current page.</param>
        public void MakeChoice (QuestChoice choice) {
            if (IsChoiceVisible (choice)) {
                this.SetCurrentPage (choice.link);
            }
        }

        /// <summary>
        /// Checks for auto execution single choice without text and process it if requires.
        /// Will return success of this check & execution.
        /// </summary>
        /// <param name="page">Page to check.</param>
        public bool MakeAutoChoice (QuestPage page) {
            if (page.choices.Count == 1 && string.IsNullOrEmpty (page.choices[0].text)) {
                SetCurrentPage (page.choices[0].link);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Set specified page as current.
        /// </summary>
        /// <param name="page">Page name.</param>
        public void SetCurrentPage (string page) {
            state.currentPage = page.ToLowerInvariant ();
        }

        /// <summary>
        /// Requests data of current page. Will return undefined if page not exists.
        /// </summary>
        public QuestPage GetCurrentPage () {
            QuestPage page;
            return pages.TryGetValue (state.currentPage, out page) ? page : null;
        }

        /// <summary>
        /// Reset actual progress to default values and entry point.
        /// </summary>
        public void ResetProgress () {
            state.currentPage = entry;
            state.vars.Clear ();
        }

        /// <summary>
        /// Returns actual progress.
        /// </summary>
        public QuestProgress GetProgress () {
            return this.state;
        }

        /// <summary>
        /// Tries to load new progress state.
        /// </summary>
        /// <param name="progress">New progress state</param>
        public void SetProgress (QuestProgress progress) {
            try {
                progress.currentPage = progress.currentPage.ToLowerInvariant ();
                if (progress.vars == null) { throw new Exception (); }
                this.state = progress;
            } catch (Exception ex) {
                throw new Exception (string.Format ("State loading error: ${0}", ex.Message));
            }
        }

        /// <summary>
        /// Returns variable value or zero if variable not exists.
        /// </summary>
        /// <param name="name">Variable name.</param>
        public int GetVariable (string name) {
            int val;
            state.vars.TryGetValue (name.ToLowerInvariant (), out val);
            return val;
        }

        /// <summary>
        /// Sets new value to specified variable.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="val">New value.</param>
        public void SetVariable (string name, int val) {
            name = name.ToLowerInvariant ();
            if (val != 0) {
                state.vars[name] = val;
            } else {
                if (state.vars.ContainsKey (name)) { state.vars.Remove (name); }
            }
        }

        /// <summary>
        /// Tries to process specified logic. Will return result of conditional logic, or false for common states.
        /// </summary>
        /// <param name="logic">Logic block to process.</param>
        public bool ProcessLogic (QuestLogic logic) {
            switch (logic.operation) {
                case "+=":
                    this.SetVariable (logic.lhs, this.GetVariable (logic.lhs) + logic.rhs);
                    return false;
                case "=":
                    this.SetVariable (logic.lhs, logic.rhs);
                    return false;
                case "==":
                    return this.GetVariable (logic.lhs) == logic.rhs;
                case "!=":
                    return this.GetVariable (logic.lhs) != logic.rhs;
                case "<":
                    return this.GetVariable (logic.lhs) < logic.rhs;
                case ">":
                    return this.GetVariable (logic.lhs) > logic.rhs;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Tries to process text to builtin logic blocks (logics will be replace with calculated values).
        /// </summary>
        /// <param name="text">Text to process.</param>
        public string ProcessText (string text) {
            return Regex.Replace (text, "\\{\\s*?(\\w+)\\s*?\\}", match => {
                var name = match.Groups[1].Value.ToLowerInvariant ();
                return this.GetVariable (name).ToString ();
            });
        }

        /// <summary>
        /// Checks choice visibility with respect optional condition.
        /// </summary>
        /// <param name="choice">Choice to test.</param>
        public bool IsChoiceVisible (QuestChoice choice) {
            return choice != null && (choice.condition == null || ProcessLogic (choice.condition));
        }
    }
}