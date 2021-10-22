using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MarkovBOT.Modules
{
    public class General : ModuleBase<SocketCommandContext>
    {
        private readonly MarkovConfig _markov;
        private readonly GuildConfig _guild;
        public General(MarkovConfig markov, GuildConfig guild)
        {
            _markov = markov;
            _guild = guild;
        }
        [Command("help"), Alias("h")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SendHelp()
        {
            var builder = new EmbedBuilder()
            .WithTitle("Here is a list of all the functionality and commands within Johnny 2.0:")
            .WithColor(new Color(0x5AC655))
            .WithImageUrl("https://cdn.discordapp.com/attachments/719958295196074089/900079538464235620/epic-embed-success-epic-embed-fail.gif")
            .WithAuthor(author =>
            {
                author
            .WithName("Help")
            .WithIconUrl("https://cdn.discordapp.com/embed/avatars/0.png");
            })
            .AddField("Seed", "Seeds the source file with X amount of messages, setting this to very high values will cause issues with discord rate limits. Successive uses of this command will simply overwrite anything previously in the source.")
            .AddField("Dump", "Deletes all stored messages in the source file.")
            .AddField("Add", "Adds X amount of messages to the source file without overwriting previously stored messages.")
            .AddField("Settings", "Shows the current markov parameters.")
            .AddField("Step", "Set the markov step value, don't change this unless you know what you are doing.")
            .AddField("Count", "Sets how many strings you want Johnny to output, the larger this value the longer the message.")
            .AddField("Chance", "Sets the % chance for Johnny to trigger, this is calculated per message sent in the server so be careful with high values.")
            .AddField("Collection", "Sets the amount of messages Johnny will automatically collect after 20 minutes")
            .AddField("Reset", "Resets all markov parameters to default, which is Step = 1, Count = 20 and Chance = 5%");
            var embed = builder.Build();
            await Context.Channel.SendMessageAsync(null, embed: embed).ConfigureAwait(false);
        }
        [Command("settings")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GetSettings()
        {
            var cfg = _guild;
            var user = Context.User as SocketGuildUser;
            var embed = new EmbedBuilder();
            embed.WithAuthor(user.Nickname ?? user.Username, user.GetAvatarUrl());
            embed.AddField("Step", $"**{cfg.Markov.Step}**", true);
            embed.AddField("Count", $"**{cfg.Markov.Count}**", true);
            embed.AddField("Chance", $"**{cfg.Markov.Chance}%**", true);
            embed.AddField("Collection", $"**{cfg.Markov.Collection}**", true);

            await ReplyAsync("", false, embed.Build());
        }
        [Command("add"), Alias("a")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AppendData(uint amount)
        {
            var messages = this.Context.Channel.GetMessagesAsync((int)amount).Flatten();
            using (StreamWriter sw = new StreamWriter("messages.csv", append: true))
            {
                await foreach (IMessage message in messages)
                {
                    sw.WriteLine(message.Content.ToString());
                }
            }
            await RemoveDuplicates();
            await ReplyAsync($":white_check_mark: Added {amount} messages to source");
        }
        [Command("dump"), Alias("d")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ClearFile()
        {
            File.Create("messages.csv").Close();
            await ReplyAsync($":white_check_mark: Markov source has been cleared");
        }
        [Command("seed"), Alias("s")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SeedFile(uint amount)
        {
            var messages = this.Context.Channel.GetMessagesAsync((int)amount).Flatten();
            using (StreamWriter sw = new StreamWriter("messages.csv"))
            {
                await foreach (IMessage message in messages)
                {
                    sw.WriteLine(message.Content.ToString());
                }
            }
            await RemoveDuplicates();
            await ReplyAsync($":white_check_mark: Seeded file with {amount} messages");
        }
        [Command("step"), Alias("st")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetStep(uint step)
        {
            if (step < 1 || step > 15)
            {
                await ReplyAsync($":negative_squared_cross_mark: Allowed markov step range: 1-15");
            }
            else
            {
                _markov.Step = step;
                await ReplyAsync($":white_check_mark: Set markov step to: {step}");
            }
        }

        [Command("count"), Alias("c")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCount(uint count)
        {
            if (count < 5 || count > 50)
            {
                await ReplyAsync($":negative_squared_cross_mark: Allowed markov count range: 5-50");
            }
            else
            {
                _markov.Count = count;
                await ReplyAsync($":white_check_mark: Set markov count to: {count}");
            }
        }

        [Command("chance"), Alias("ch")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetChance(uint chance)
        {
            if (chance > 100)
            {
                chance = 100;
            }
            _markov.Chance = chance;
            await ReplyAsync($":white_check_mark: Set markov chance to: {chance}%");
        }
        [Command("collection"), Alias("co")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCollection(uint amount)
        {
            if (amount > 300)
            {
                await ReplyAsync($":negative_squared_cross_mark: Cannot set collection value greater than 300");
            }
            else
            {
                _markov.Collection = amount;
                await ReplyAsync($":white_check_mark: Set markov collection to: {amount}");
            }
        }

        [Command("reset"), Alias("r")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Reset()
        {
            _guild.Markov = new MarkovConfig();
            await ReplyAsync($":white_check_mark: Reset markov settings");
        }
        public async Task RemoveDuplicates()
        {
            string[] lines = File.ReadAllLines("messages.csv");
            await File.WriteAllLinesAsync("messages.csv", lines.Distinct().ToArray());
        }
    }
}
