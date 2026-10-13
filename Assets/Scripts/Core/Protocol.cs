using System;
namespace DustAndPowder
{
    [Serializable] public class Command
    {
        public string op = "", name = "Игрок", code = "", token = "", signal = "";
        public int seats = 5, seconds = 45, seq, revision, target = -1;
        public bool ready, simple; public Plan plan;
    }
    [Serializable] public class Envelope
    {
        public string type = "state", error = "", token = "", code = "";
        public int ack, revision; public MatchView view;
    }
}
