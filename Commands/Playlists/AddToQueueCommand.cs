// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Music;

internal sealed class AddToQueueCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForHeavy(ctx))
                return;

            var playlistId = (string)arguments["playlist"];

            if (!MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Any(x => x.PlaylistId == playlistId))
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.NoPlaylist, true),
                }.AsError(ctx, this.GetString(CommandKey.Playlists.Title)));
                return;
            }

            var SelectedPlaylist = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.First(x => x.PlaylistId == playlistId);

            _ = await this.RespondOrEdit(new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.Play.Preparing, true),
            }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title)));

            try
            {
                await new JoinCommand().TransferCommand(ctx, null);
            }
            catch (CancelException)
            {
                this.DeleteOrInvalidate();
                return;
            }

            _ = await this.RespondOrEdit(new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.Playlists.AddToQueue.Adding, true,
                new TVar("Name", SelectedPlaylist.PlaylistName),
                new TVar("", SelectedPlaylist.List.Length)),
            }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title)));

            MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue = MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.AddRange(SelectedPlaylist.List.Select(x => new GuildMusic.QueueInfo(x.Title, x.Url, x.Length.Value, ctx.Guild.Id, ctx.User.Id)));

            _ = await this.RespondOrEdit(new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.Play.QueuedMultiple, true,
                new TVar("Count", SelectedPlaylist.List.Length),
                new TVar("Playlist", SelectedPlaylist.PlaylistName))
            }
            .AddField(new DiscordEmbedField($"📜 {this.GetString(CommandKey.Play.QueuePositions)}", $"{(MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length - SelectedPlaylist.List.Length + 1)} - {MusicPlugin.Plugin!.Guilds![ctx.Guild.Id].SongQueue.Length}"))
            .AsSuccess(ctx, this.GetString(CommandKey.Playlists.Title)));
        });
    }
}