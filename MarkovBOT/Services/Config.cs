namespace MarkovBOT
{
    public class GuildConfig
    {
        public MarkovConfig Markov { get; set; } = new MarkovConfig();
    }
    public class MarkovConfig
    {
        public uint Step { get; set; } = 1;
        public uint Count { get; set; } = 20;
        public uint Collection { get; set; } = 100;
        public uint Chance { get; set; } = 3;
    }
}
