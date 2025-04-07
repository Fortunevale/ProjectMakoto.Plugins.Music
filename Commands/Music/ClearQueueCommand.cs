// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Music;

internal sealed class ClearQueueCommand : BaseCommand
{
    public override Task<bool> BeforeExecution(SharedCommandContext ctx) => this.CheckVoiceState();

    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForHeavy(ctx))
                return;

            var lava = ctx.Client.GetLavalink();
            var session = lava.ConnectedSessions.Values.First(x => x.IsConnected);
            var conn = session.GetGuildPlayer(ctx.Member.VoiceState.Guild);

            if (conn is null || conn.Channel.Id != ctx.Member.VoiceState.Channel.Id)
            {
                _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.NotSameChannel, true),
                }.AsError(ctx));
                return;
            }

            if (MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Contains(ctx.User.Id))
            {
                _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.ClearQueue.AlreadyVoted, true),
                }.AsError(ctx));
                return;
            }

            MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Add(ctx.User.Id);

            if (MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Count >= (conn.Channel.Users.Count - 1) * 0.51)
            {
                MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = [];
                MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Clear();

                _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.ClearQueue.Cleared, true),
                }.AsSuccess(ctx));
                return;
            }

            var embed = new DiscordEmbedBuilder()
            {
                Description = $"`{this.GetGuildString(CommandKey.ClearQueue.VoteStarted)} ({MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Count}/{Math.Ceiling((conn.Channel.Users.Count - 1.0) * 0.51)})`",
            }.AsAwaitingInput(ctx);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);

            DiscordButtonComponent DisconnectVote = new(ButtonStyle.Danger, Guid.NewGuid().ToString(), this.GetGuildString(CommandKey.ClearQueue.VoteButton), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🗑")));
            _ = builder.AddComponents(DisconnectVote);

            _ = await this.RespondOrEdit(builder);

            _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(x =>
            {
                if (x.IsCompletedSuccessfully)
                {
                    ctx.Client.ComponentInteractionCreated -= RunInteraction;
                    this.ModifyToTimedOut();
                }
            });

            ctx.Client.ComponentInteractionCreated += RunInteraction;

            async Task RunInteraction(DiscordClient s, ComponentInteractionCreateEventArgs e)
            {
                _ = Task.Run(async () =>
                {
                    if (e.Message.Id == ctx.ResponseMessage.Id)
                    {
                        _ = e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                        if (MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Contains(e.User.Id))
                        {
                            _ = e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent($"❌ {this.GetString(CommandKey.ClearQueue.AlreadyVoted, true)}").AsEphemeral());
                            return;
                        }

                        var member = await e.User.ConvertToMember(ctx.Guild);

                        if (member.VoiceState is null || member.VoiceState.Channel.Id != conn.Channel.Id)
                        {
                            _ = e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent($"❌ {this.GetString(CommandKey.NotSameChannel, true)}").AsEphemeral());
                            return;
                        }

                        MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Add(e.User.Id);

                        if (MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Count >= (conn.Channel.Users.Count - 1) * 0.51)
                        {
                            MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = [];
                            MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Clear();

                            _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                            {
                                Description = this.GetString(CommandKey.ClearQueue.Cleared, true),
                            }.AsSuccess(ctx)));
                            return;
                        }

                        embed.Description = $"`{this.GetGuildString(CommandKey.ClearQueue.VoteStarted)} ({MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].collectedClearQueueVotes.Count}/{Math.Ceiling((conn.Channel.Users.Count - 1.0) * 0.51)})`";
                        _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(embed).AddComponents(DisconnectVote));
                    }
                }).Add(ctx.Bot);
            }
        });
    }
}