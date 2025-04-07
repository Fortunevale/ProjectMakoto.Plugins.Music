// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using ProjectMakoto.Entities.Users;

namespace ProjectMakoto.Plugins.Music;

internal sealed class LoadShareCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            var userid = ((DiscordUser)arguments["user"]).Id;
            var id = ((string)arguments["id"])
                .Replace("/", "")
                .Replace("\\", "")
                .Replace(">", "")
                .Replace("<", "")
                .Replace("|", "")
                .Replace(":", "")
                .Replace("&", "");

            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            var embed = new DiscordEmbedBuilder()
            {
                Description = this.GetString(CommandKey.Playlists.LoadShare.Loading, true)
            }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title));
            _ = await this.RespondOrEdit(embed);

            if (!Directory.Exists("PlaylistShares"))
                _ = Directory.CreateDirectory("PlaylistShares");

            if (!Directory.Exists($"PlaylistShares/{userid}") || !File.Exists($"PlaylistShares/{userid}/{id}.json"))
            {
                embed.Description = this.GetString(CommandKey.Playlists.LoadShare.NotFound);
                _ = embed.AsError(ctx, this.GetString(CommandKey.Playlists.Title));
                _ = await this.RespondOrEdit(embed.Build());
                return;
            }

            var user = await ctx.Client.GetUserAsync(userid);

            var rawJson = File.ReadAllText($"PlaylistShares/{userid}/{id}.json");
            var ImportJson = JsonConvert.DeserializeObject<UserPlaylist>((rawJson is null or "null" or "" ? "[]" : rawJson), new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });

            var pad = TranslationUtil.CalculatePadding(ctx.DbUser, CommandKey.Playlists.LoadShare.PlaylistName, CommandKey.Playlists.Tracks, CommandKey.Playlists.LoadShare.CreatedBy);

            _ = embed.AsInfo(ctx, this.GetString(CommandKey.Playlists.Title));
            embed.Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ImportJson.PlaylistThumbnail };
            embed.Color = (ImportJson.PlaylistColor is "#FFFFFF" or null or "" ? EmbedColors.Info : new DiscordColor(ImportJson.PlaylistColor.IsValidHexColor()));
            embed.Description = $"{this.GetString(CommandKey.Playlists.LoadShare.Found, true)}\n\n" +
                                $"`{this.GetString(CommandKey.Playlists.LoadShare.PlaylistName).PadRight(pad)}`: `{ImportJson.PlaylistName}`\n" +
                                $"`{this.GetString(CommandKey.Playlists.Tracks).PadRight(pad)}`: `{ImportJson.List.Length}`\n" +
                                $"`{this.GetString(CommandKey.Playlists.LoadShare.CreatedBy).PadRight(pad)}`: {user.Mention} `{user.GetUsernameWithIdentifier()} ({user.Id})`";

            DiscordButtonComponent Confirm = new(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.LoadShare.ImportButton), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📥")));

            _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(embed).AddComponents(new List<DiscordComponent> { Confirm, MessageComponents.GetCancelButton(ctx.DbUser, ctx.Bot) }));

            var e = await ctx.WaitForButtonAsync(TimeSpan.FromMinutes(1));

            if (e.TimedOut)
            {
                this.ModifyToTimedOut();
                return;
            }

            _ = e.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);


            if (e.GetCustomId() == Confirm.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.LoadShare.Importing, true),
                }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title))));

                if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
                {
                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                    }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                    return;
                }

                MusicPlugin.Plugin.Users[ctx.User.Id].Playlists = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Add(ImportJson);

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.LoadShare.Imported, true, new TVar("Name", ImportJson.PlaylistName)),
                }.AsSuccess(ctx, this.GetString(CommandKey.Playlists.Title))));
            }
            else
            {
                this.DeleteOrInvalidate();
            }
        });
    }
}