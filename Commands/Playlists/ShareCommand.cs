// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Music;

internal sealed class ShareCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
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

            var ShareCode = $"{Guid.NewGuid()}";

            if (!Directory.Exists("PlaylistShares"))
                _ = Directory.CreateDirectory("PlaylistShares");

            if (!Directory.Exists($"PlaylistShares/{ctx.User.Id}"))
                _ = Directory.CreateDirectory($"PlaylistShares/{ctx.User.Id}");

            _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
            {
                Description = this.GetString(CommandKey.Playlists.Share.Shared, true,
                new TVar("Command", $"{ctx.Prefix}playlists load-share {ctx.User.Id} {ShareCode}")),
            }.AsInfo(ctx, this.GetString(CommandKey.Playlists.Title))));

            File.WriteAllText($"PlaylistShares/{ctx.User.Id}/{ShareCode}.json", JsonConvert.SerializeObject(SelectedPlaylist, Formatting.Indented));
        });
    }
}