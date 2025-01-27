// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Music;

internal sealed class ExportCommand : BaseCommand
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

            using (MemoryStream stream = new(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(SelectedPlaylist, Formatting.Indented))))
            {
                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder()
                {
                    Description = this.GetString(CommandKey.Playlists.Export.Exported, true, new TVar("Name", SelectedPlaylist.PlaylistName)),
                }.AsInfo(ctx, this.GetString(CommandKey.Playlists.Title))).WithFile($"{Guid.NewGuid().ToString().Replace("-", "").ToLower()}.json", stream));
            }
        });
    }
}