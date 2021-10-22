using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MarkovBOT.Services
{

    public class CommandHandler : DiscordClientService
    {

        private readonly IServiceProvider provider;
        private readonly DiscordSocketClient client;
        private readonly CommandService service;
        private readonly IConfiguration configuration;
        private readonly MarkovConfig markov;
        private static int Interval = 1200;
        private System.Timers.Timer t = new System.Timers.Timer(1000 * Interval);

        public CommandHandler(IServiceProvider provider, DiscordSocketClient client, CommandService service, IConfiguration configuration, ILogger<CommandHandler> logger, MarkovConfig markov) : base(client, logger)
        {
            this.provider = provider;
            this.client = client;
            this.service = service;
            this.configuration = configuration;
            this.markov = markov;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.client.Ready += StartTimer;
            this.client.MessageReceived += OnMessageReceived;
            await this.service.AddModulesAsync(Assembly.GetEntryAssembly(), this.provider);
        }
        private async Task OnMessageReceived(SocketMessage socketMessage)
        {
            if (!(socketMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            var argPos = 0;
            var context = new SocketCommandContext(this.client, message);
            if (!message.HasStringPrefix(this.configuration["Prefix"], ref argPos) && !message.HasMentionPrefix(this.client.CurrentUser, ref argPos))
            {
                await MarkovOutput(context);
            }
            else
            {
                await this.service.ExecuteAsync(context, argPos, this.provider);
            }
        }
        private async Task MarkovOutput(SocketCommandContext context)
        {
            if (new Random(DateTime.UtcNow.Millisecond).Next(1, 100 + 1) <= markov.Chance)
            {
                using (context.Channel.EnterTypingState())
                {
                    await MarkovTalk(context, (int)markov.Step, (int)markov.Count);
                }
            }
        }
        private async Task MarkovTalk(SocketCommandContext ctx, int step, int wordCount)
        {
            var message = File.ReadLines("messages.csv");
            var messages = message.Select(x => x.ToString()).ToList();
            if (!message.Any()) return;

            var filtered = FilterMessages(ctx, messages);
            if (!filtered.Any()) return;

            var chain = MakeChain(filtered, step);
            if (!chain.Any()) return;

            var result = GenerateMessage(chain, step, wordCount);
            if (string.IsNullOrEmpty(result)) return;

            await ctx.Channel.SendMessageAsync(result);
        }
        private List<string> FilterMessages(SocketCommandContext ctx, List<string> messages)
        {
            var control = @"[!?.,:;()[]]+";
            var prefix = $"^{this.configuration["Prefix"]}\\w+";
            var filter = @"";
            var filtered = new List<string>();

            foreach (var msg in messages)
            {
                var rep = Regex.Replace(msg, filter, "");
                if (string.IsNullOrEmpty(rep)) continue;

                var sb = new StringBuilder();
                foreach (var s in rep.Select(x => x.ToString()))
                {
                    sb.Append(Regex.IsMatch(s, control) ? $" {s} " : s);
                }

                var split = Regex.Split(sb.ToString(), @"\s+");
                if (!split.Any()) continue;

                var noEmpty = split.Where(x => !string.IsNullOrEmpty(x));
                if (!noEmpty.Any()) continue;

                filtered.AddRange(noEmpty);
            }
            return filtered;
        }
        private Dictionary<string, List<string>> MakeChain(List<string> filtered, int step)
        {
            var chain = new Dictionary<string, List<string>>();
            for (var i = 0; i < filtered.Count - step; i++)
            {
                var k = string.Join(" ", filtered.Skip(i).Take(step));
                var v = filtered[i + step];
                if (!chain.ContainsKey(k))
                {
                    chain.Add(k, new List<string> { v });
                }
                else
                {
                    chain[k].Add(v);
                }
            }
            return chain;
        }
        private string GenerateMessage(Dictionary<string, List<string>> chain, int step, int wordCount)
        {
            var control = @"[!?.,:;()[]]+";
            var rand = new Random(DateTime.UtcNow.Millisecond);
            var result = new StringBuilder();
            var temp = new List<string>
      {
        chain.ElementAt(rand.Next(0, chain.Count)).Key,
      };
            for (int i = 0; i < wordCount; i++)
            {
                var key = string.Join(" ", temp.Skip(i).Take(step));
                if (!chain.ContainsKey(key))
                {
                    key = chain.ElementAt(rand.Next(0, chain.Count)).Key;
                }
                var value = chain[key].ElementAt(rand.Next(0, chain[key].Count));
                while (result.Length == 0 && Regex.IsMatch(value, control))
                {
                    key = chain.ElementAt(rand.Next(0, chain.Count)).Key;
                    value = chain[key].ElementAt(rand.Next(0, chain[key].Count));
                }
                temp.Add(value);
                result.Append(Regex.IsMatch(value, control) ? value : $" {value}");
            }
            return result.ToString();
        }
        private async Task StartTimer()
        {
            t.AutoReset = true;
            t.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            t.Start();
        }
        private async void OnTimedEvent(Object sender, ElapsedEventArgs e)
        {
            ulong id = 620421092347084810;
            var channel = client.GetChannel(id) as IMessageChannel;
            var messages = channel.GetMessagesAsync((int)markov.Collection).Flatten();
            using (StreamWriter sw = new StreamWriter("messages.csv", append: true))
            {
                await foreach (IMessage message in messages)
                {
                    sw.WriteLine(message.Content.ToString());
                }
            }
            await RemoveDuplicates();
            await channel.SendMessageAsync(":white_check_mark: Collection complete, duplicates terminated");
        }
        private async Task RemoveDuplicates()
        {
            string[] lines = File.ReadAllLines("messages.csv");
            await File.WriteAllLinesAsync("messages.csv", lines.Distinct().ToArray());
        }
    }
}

