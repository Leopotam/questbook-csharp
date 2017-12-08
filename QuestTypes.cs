using System;
using System.Collections.Generic;

namespace LeopotamGroup.Questbook {
    /// <summary>
    /// Description of logic block data.
    /// </summary>
    [Serializable]
    public class QuestLogic {
        public string operation;
        public string lhs;
        public int rhs;
    }

    /// <summary>
    /// Description of choice data.
    /// </summary>
    [Serializable]
    public class QuestChoice {
        public string text;
        public string link = string.Empty;
        public QuestLogic condition;
    }

    /// <summary>
    /// Description of page data.
    /// </summary>
    [Serializable]
    public class QuestPage {
        public List<string> texts = new List<string> ();
        public List<QuestChoice> choices = new List<QuestChoice> ();
        public List<QuestLogic> logics;
    }

    /// <summary>
    /// Description of progress data.
    /// </summary>
    [Serializable]
    public class QuestProgress {
        public string currentPage = string.Empty;
        public Dictionary<string, int> vars = new Dictionary<string, int> ();
    }
}