// Project Makoto Example Plugin
// Copyright (C) 2023 Fortunevale
// This code is licensed under MIT license (see 'LICENSE'-file for details)

using System.Linq;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using Newtonsoft.Json;
using ProjectMakoto.Database;
using ProjectMakoto.Enums;
using Xorog.UniversalExtensions;

namespace ProjectMakoto.Plugins.Music.Entities;


[TableName("users")]
public class UserMusic : PluginDatabaseTable
{
    public UserMusic(BasePlugin plugin, ulong identifierValue) : base(plugin, identifierValue)
    {
        this.Id = identifierValue;
    }

    [ColumnName("UserId"), ColumnType(ColumnTypes.BigInt), Primary]
    internal ulong Id { get; init; }

    [ColumnName("Playlists"), ColumnType(ColumnTypes.LongText), Default("[]")]
    public UserPlaylist[] Playlists
    { 
        get => (JsonConvert.DeserializeObject<UserPlaylist[]>(this.GetValue<string>(this.Id, "Playlists")) ?? [])
            .Select(x =>
            {
                x.Bot = this.Bot;
                x.Parent = this;

                return x;
            }).ToArray();
        set => this.SetValue(this.Id, "Playlists", JsonConvert.SerializeObject(value));
    }
}
