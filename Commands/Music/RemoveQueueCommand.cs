// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using ProjectMakoto.Entities.Guilds;
using Xorog.UniversalExtensions;

namespace ProjectMakoto.Plugins.Music;

internal sealed class RemoveQueueCommand : BaseCommand
{
    public override Task<bool> BeforeExecution(SharedCommandContext ctx) => this.CheckVoiceState();

    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            var selection = (string)arguments["video"];

            if (string.IsNullOrWhiteSpace(selection))
            {
                this.SendSyntaxError();
                return;
            }

            if (await ctx.DbUser.Cooldown.WaitForLight(ctx))
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

            GuildMusic.QueueInfo info = null;

            if (selection.IsDigitsOnly())
            {
                var Index = Convert.ToInt32(selection) - 1;

                if (Index < 0 || Index >= MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length)
                {
                    _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.RemoveQueue.OutOfRange, true, new TVar("Min", 1), new TVar("Max", MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length)),
                    }.AsError(ctx));
                    return;
                }

                info = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue[Index];
            }
            else
            {
                if (!MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Any(x =>  x.VideoTitle.ToLower() == selection.ToLower()))
                {
                    _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.RemoveQueue.NoSong, true),
                    }.AsError(ctx));
                    return;
                }

                info = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.First(x => x.VideoTitle.ToLower() == selection.ToLower());
            }

            if (info is null)
            {
                _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.RemoveQueue.NoSong, true),
                }.AsError(ctx));
                return;
            }

            MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Remove(x => x.UUID, info);

            _ = await this.RespondOrEdit(embed: new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.RemoveQueue.Removed, true, new TVar("Track", $"`[`{info.VideoTitle}`]({info.Url})`")),
            }.AsSuccess(ctx));
        });
    }
}