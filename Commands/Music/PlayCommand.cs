// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using DisCatSharp.Lavalink.Entities;
using Xorog.UniversalExtensions;
using Xorog.UniversalExtensions.Enums;

namespace ProjectMakoto.Plugins.Music;

internal sealed class PlayCommand : BaseCommand
{
    public override async Task<bool> BeforeExecution(SharedCommandContext ctx) => (await this.CheckVoiceState() && await this.CheckOwnPermissions(Permissions.UseVoice) && await this.CheckOwnPermissions(Permissions.UseVoiceDetection));

    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            var search = (string)arguments["search"];

            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            if (search.IsNullOrWhiteSpace())
            {
                this.SendSyntaxError();
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.Play.Preparing, true),
            }.AsLoading(ctx);
            _ = await this.RespondOrEdit(embed);

            try
            {
                await new JoinCommand().TransferCommand(ctx, null);
            }
            catch (CancelException)
            {
                return;
            }

            var (Tracks, oriResult, Continue) = await MusicModuleAbstractions.GetLoadResult(ctx, search);


            embed.Author.IconUrl = ctx.Guild.IconUrl;

            if (!Continue || !Tracks.IsNotNullAndNotEmpty())
            {
                return;
            }

            _ = await this.RespondOrEdit(embed);

            try
            {
                await new JoinCommand().TransferCommand(ctx, null);
            }
            catch (CancelException)
            {
                return;
            }

            if (Tracks.Count > 1)
            {
                var added = 0;

                foreach (var b in Tracks)
                {
                    added++;
                    MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Add(new(b.Info.Title, b.Info.Uri.ToString(), b.Info.Length, ctx.Guild.Id, ctx.User.Id));
                }

                embed.Description = this.GetString(CommandKey.Play.QueuedMultiple, true,
                    new TVar("Count", added),
                    new TVar("Playlist", new EmbeddedLink(search, oriResult.GetResultAs<LavalinkPlaylist>().Info.Name)));

                _ = embed.AddField(new DiscordEmbedField($"📜 {this.GetString(CommandKey.Play.QueuePositions)}", $"{(MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length - added + 1)} - {MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length}", true));

                _ = embed.AsSuccess(ctx);
                _ = await ctx.BaseCommand.RespondOrEdit(embed);
            }
            else if (Tracks.Count == 1)
            {
                var track = Tracks[0];

                MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Add(new(track.Info.Title, track.Info.Uri.ToString(), track.Info.Length, ctx.Guild.Id, ctx.User.Id));

                embed.Description = this.GetString(CommandKey.Play.QueuedSingle, true,
                    new TVar("Track", new EmbeddedLink(track.Info.Uri.ToString(), track.Info.Title)));

                _ = embed.AddField(new DiscordEmbedField($"📜 {this.GetString(CommandKey.Play.QueuePosition)}", $"{MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length}", true));
                _ = embed.AddField(new DiscordEmbedField($"🔼 {this.GetString(CommandKey.Play.Uploader)}", $"{track.Info.Author}", true));
                _ = embed.AddField(new DiscordEmbedField($"🕒 {this.GetString(CommandKey.Play.Duration)}", $"{track.Info.Length.GetHumanReadable(TimeFormat.Minutes)}", true));

                _ = embed.AsSuccess(ctx);
                _ = await ctx.BaseCommand.RespondOrEdit(embed.Build());
            }
        });
    }
}