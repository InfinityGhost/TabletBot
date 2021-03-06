using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Octokit;
using TabletBot.Discord.Embeds;
using TabletBot.GitHub;

namespace TabletBot.Discord.Commands
{
    public class GitHubCommands : CommandModule
    {
        private GitHubAPI GitHub => TabletBot.GitHub.GitHubAPI.Current;
        private const string RepositoryOwner = "InfinityGhost";
        private const string RepositoryName = "OpenTabletDriver";
        private static readonly Regex ArtifactRegex = new Regex("<.+?href=\"/InfinityGhost/OpenTabletDriver/suites/(?<Suite>.+?)/artifacts/(?<Artifact>.+?)\">(?<Name>.+?)</.+?>");
        private static readonly Regex CommitRegex = new Regex("href=\".+?commit/(?<SHA>.+?)/.+?/.+?\"");

        [Command("overview", RunMode = RunMode.Async), Name("Overview"), Alias("info"), Summary("Shows an overview of the repository.")]
        public async Task GetRepositoryOverview()
        {
            var message = await ReplyAsync($"Getting overview for {RepositoryOwner}/{RepositoryName}...");
            var repo = await GitHub.Repository.Get(RepositoryOwner, RepositoryName);

            IEnumerable<Issue> issues =
                from issue in await GitHub.Issue.GetAllForRepository(repo.Id)
                where issue.State == ItemState.Open
                select issue;

            IEnumerable<PullRequest> prs =
                from pr in await GitHub.PullRequest.GetAllForRepository(repo.Id)
                where pr.State == ItemState.Open
                select pr;

            var embed = new EmbedBuilder
            {
                Title = $"{RepositoryOwner}/{RepositoryName}",
                Timestamp = repo.PushedAt,
                Url = repo.HtmlUrl,
                ThumbnailUrl = Bot.Current.DiscordClient.CurrentUser.GetAvatarUrl(),
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Last pushed {repo.PushedAt}",
                    IconUrl = repo.Owner.AvatarUrl
                }
            };
            embed.AddField("Open issues", issues.Count());
            embed.AddField("Open pull requests", prs.Count());
            await message.Update(embed);
        }

        [Command("getpr", RunMode = RunMode.Async), Name("Get Pull Request"), Alias("pr"), Summary("Fetches pull request information.")]
        public async Task GetPullRequest([Remainder] int id)
        {
            await Context.Message.DeleteAsync();
            var message = await ReplyAsync($"Fetching pull request #{id}");
            var pr = await GitHub.PullRequest.Get(RepositoryOwner, RepositoryName, id);
            var embed = GitHubEmbeds.GetPullRequestEmbed(pr);
            await message.Update(embed);
        }

        [Command("getissue", RunMode = RunMode.Async), Name("Get Issue"), Alias("issue"), Summary("Fetches issue information.")]
        public async Task GetIssue([Remainder] int id)
        {
            await Context.Message.DeleteAsync();
            var message = await ReplyAsync($"Fetching issue #{id}");
            var issue = await GitHub.Issue.Get(RepositoryOwner, RepositoryName, id);
            var embed = GitHubEmbeds.GetIssueEmbed(issue);
            await message.Update(embed);
        }

        [Command("getartifacts", RunMode = RunMode.Async), Name("Get Artifacts"), Alias("artifacts", "artifact"), Summary("Returns all artifacts for a workflow from its URL.")]
        public async Task GetArtifacts([Remainder] string url)
        {
            await Context.Message.DeleteAsync();
            var message = await ReplyAsync("Fetching artifacts...");

            string html;
            using (var client = new HttpClient())
                html = await client.GetStringAsync(url);

            IEnumerable<Match> artifacts = await Task<MatchCollection>.Run(() => ArtifactRegex.Matches(html));
            Match commitMatch = await Task<Match>.Run(() => CommitRegex.Match(html));
            string sha = commitMatch.Groups["SHA"].Value;
            string hash = string.Concat(sha.Take(7));
            var commit = await GitHub.Git.Commit.Get(RepositoryOwner, RepositoryName, sha);
            var title = commit.Message.Split(Environment.NewLine).First();
            
            var embed = new EmbedBuilder
            {
                Title = string.Format("{0} ({1})", title, hash),
                Url = url,
                Color = Color.Green
            };

            foreach (var artifact in artifacts)
            {
                var suite = Convert.ToInt32(artifact.Groups["Suite"].Value);
                var id = Convert.ToInt32(artifact.Groups["Artifact"].Value);
                var name = artifact.Groups["Name"].Value;
                embed.AddField(name, string.Format("https://github.com/InfinityGhost/OpenTabletDriver/suites/{0}/artifacts/{1}", suite, id));
            }

            await message.Update(embed);
        }
    }
}