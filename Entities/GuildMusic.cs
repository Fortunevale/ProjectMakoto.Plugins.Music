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


[TableName("guilds")]
public class GuildMusic : PluginDatabaseTable
{
    public GuildMusic(BasePlugin plugin, ulong identifierValue) : base(plugin, identifierValue)
    {
        this.Id = identifierValue;
    }

    [ColumnName("GuildId"), ColumnType(ColumnTypes.BigInt), Primary]
    internal ulong Id { get; init; }

    public void Reset()
    {
        this.SongQueue = [];
        this.ChannelId = 0;
        this.CurrentVideo = null;
        this.CurrentVideoPosition = -1;
        this.Repeat = false;
        this.Shuffle = false;
        this.IsPaused = false;

        this.Disposed = false;
    }

    private DiscordGuild Guild { get; set; }

    public List<ulong> collectedSkips = [];
    public List<ulong> collectedDisconnectVotes = [];
    public List<ulong> collectedClearQueueVotes = [];

    [ColumnName("SongQueue"), ColumnType(ColumnTypes.LongText), Default("[]")]
    public QueueInfo[] SongQueue
    {
        get => JsonConvert.DeserializeObject<QueueInfo[]>(this.GetValue<string>(this.Id, "SongQueue")) ?? [];
        set => _ = this.SetValue(this.Id, "SongQueue", JsonConvert.SerializeObject(value));
    }

    [ColumnName("Channel"), ColumnType(ColumnTypes.BigInt), Default("0")]
    public ulong ChannelId
    {
        get => this.GetValue<ulong>(this.Id, "Channel");
        set => _ = this.SetValue(this.Id, "Channel", value);
    }

    [ColumnName("CurrentVideo"), ColumnType(ColumnTypes.Text), Nullable]
    public string? CurrentVideo
    {
        get => this.GetValue<string>(this.Id, "CurrentVideo");
        set => _ = this.SetValue(this.Id, "CurrentVideo", value ?? string.Empty);
    }

    [ColumnName("CurrentPosition"), ColumnType(ColumnTypes.BigInt), Default("-1")]
    public long CurrentVideoPosition
    {
        get => this.GetValue<long>(this.Id, "CurrentPosition");
        set => _ = this.SetValue(this.Id, "CurrentPosition", value);
    }

    [ColumnName("Repeat"), ColumnType(ColumnTypes.TinyInt), Default("0")]
    public bool Repeat
    {
        get => this.GetValue<bool>(this.Id, "Repeat");
        set => _ = this.SetValue(this.Id, "Repeat", value);
    }

    [ColumnName("Shuffle"), ColumnType(ColumnTypes.TinyInt), Default("0")]
    public bool Shuffle
    {
        get => this.GetValue<bool>(this.Id, "Shuffle");
        set => _ = this.SetValue(this.Id, "Shuffle", value);
    }

    [ColumnName("Paused"), ColumnType(ColumnTypes.TinyInt), Default("0")]
    public bool IsPaused
    {
        get => this.GetValue<bool>(this.Id, "Paused");
        set => _ = this.SetValue(this.Id, "Paused", value);
    }

    public sealed class QueueInfo(string VideoTitle, string Url, TimeSpan length, ulong? guild, ulong? user)
    {
        public string UUID { get; set; } = Guid.NewGuid().ToString();

        public string VideoTitle { get; set; } = VideoTitle;
        public string Url { get; set; } = Url;

        public TimeSpan Length { get; set; } = length;

        public ulong GuildId = guild ?? 0;
        public ulong UserId = user ?? 0;
    }

    public bool Disposed { private set; get; } = false;
    public bool Initialized { private set; get; } = false;

    public void Dispose(Bot _bot, ulong Id, string reason)
    {
        this.Disposed = true;

        MusicPlugin.Plugin!._logger.LogDebug("Disposed Player for {Id}. ({reason})", Id, reason);

        MusicPlugin.Plugin.Guilds![Id].Reset();
    }

    public void QueueHandler(Bot _bot, DiscordClient sender, LavalinkSession session, LavalinkGuildPlayer guildPlayer)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (this.Initialized || this.Disposed)
                    return;

                this.Initialized = true;
                this.Guild = guildPlayer.Guild;

                MusicPlugin.Plugin!._logger.LogDebug("Initializing Player for {Guild}..", this.Guild.Id);

                var UserAmount = guildPlayer.Channel.Users.Count;
                CancellationTokenSource VoiceUpdateTokenSource = new();
                Task VoiceStateUpdated(DiscordClient s, VoiceStateUpdateEventArgs e)
                {
                    if (e.Guild is null || e.Guild?.Id != this.Guild?.Id)
                        return Task.CompletedTask;

                    _ = Task.Run(() =>
                    {
                        if (e.Channel?.Id == guildPlayer.Channel?.Id || e.Before?.Channel?.Id == guildPlayer.Channel?.Id)
                        {
                            VoiceUpdateTokenSource.Cancel();
                            VoiceUpdateTokenSource = new();

                            UserAmount = e.Channel is not null ? e.Channel.Users.Count : e.Guild!.Channels.First(x => x.Key == e.Before?.Channel?.Id).Value.Users.Count;

                            MusicPlugin.Plugin!._logger.LogTrace("UserAmount updated to {UserAmount} for {Guild}", UserAmount, this.Guild!.Id);

                            if (UserAmount <= 1)
                                _ = Task.Delay(30000, VoiceUpdateTokenSource.Token).ContinueWith(x =>
                                {
                                    if (!x.IsCompletedSuccessfully)
                                        return;

                                    if (this.Disposed)
                                        return;

                                    if (UserAmount <= 1)
                                    {
                                        MusicPlugin.Plugin.Guilds![this.Id].Dispose(_bot, e.Guild!.Id, "No users");
                                        MusicPlugin.Plugin.Guilds![this.Id].Reset();
                                    }
                                });
                        }

                        return Task.CompletedTask;
                    }).Add(_bot);

                    _ = Task.Run(() =>
                    {
                        if (e.User.Id == sender.CurrentUser.Id)
                        {
                            if (e.After is null || e.After.Channel is null)
                            {
                                _ = guildPlayer.DisconnectAsync();
                                this.Dispose(_bot, e.Guild!.Id, "Disconnected");
                                return Task.CompletedTask;
                            }
                        }

                        return Task.CompletedTask;
                    }).Add(_bot);

                    return Task.CompletedTask;
                }

                Task StateUpdated(LavalinkGuildPlayer sender, LavalinkPlayerStateUpdateEventArgs e)
                {
                    this.CurrentVideo = (sender.CurrentTrack?.Info?.Uri ?? new UriBuilder().Uri).ToString();
                    this.CurrentVideoPosition = (Convert.ToInt64(e.State?.Position.TotalSeconds ?? -1d));

                    return Task.CompletedTask;
                }

                MusicPlugin.Plugin!._logger.LogDebug("Initializing VoiceStateUpdated Event for {Guild}..", this.Guild.Id);
                sender.VoiceStateUpdated += VoiceStateUpdated;

                MusicPlugin.Plugin!._logger.LogDebug("Initializing PlayerUpdated Event for {Guild}..", this.Guild.Id);
                guildPlayer.StateUpdated += StateUpdated;

                QueueInfo? LastPlayedTrack = null;

                while (true)
                {
                    var WaitSeconds = 30;

                    while ((guildPlayer!.CurrentTrack is not null || MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Length <= 0) && !this.Disposed)
                    {
                        if (guildPlayer.CurrentTrack is null && MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Length <= 0)
                        {
                            WaitSeconds--;

                            if (WaitSeconds <= 0)
                                break;
                        }

                        await Task.Delay(1000);
                    }

                    if (this.Disposed)
                    {
                        sender.VoiceStateUpdated -= VoiceStateUpdated;
                        guildPlayer.StateUpdated -= StateUpdated;

                        _ = guildPlayer.DisconnectAsync();

                        this.Dispose(this.Bot, this.Id, "Graceful Disconnect");
                        return;
                    }

                    if (WaitSeconds <= 0)
                        this.Dispose(_bot, this.Guild.Id, "Time out, nothing playing");

                    QueueInfo Track;

                    var skipSongs = 0;

                    if (LastPlayedTrack is not null &&
                        MusicPlugin.Plugin.Guilds![this.Guild.Id].Repeat &&
                        MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.IsNotNullAndNotEmpty() &&
                        MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Contains(LastPlayedTrack))
                    {
                        skipSongs = Array.IndexOf(MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue, LastPlayedTrack) + 1;

                        if (skipSongs >= MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Length)
                            skipSongs = 0;
                    }

                    if (this.SongQueue.Length <= 0)
                    {
                        this.Dispose(_bot, this.Guild.Id, "Queue empty");
                        continue;
                    }

                    Track = MusicPlugin.Plugin.Guilds![this.Guild.Id].Shuffle
                        ? MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.OrderBy(_ => Guid.NewGuid()).ToList().First()
                        : MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.ToList().Skip(skipSongs).First();

                    LastPlayedTrack = Track;

                    MusicPlugin.Plugin.Guilds![this.Guild.Id].collectedSkips.Clear();

                    var loadResult = await session.LoadTracksAsync(LavalinkSearchType.Plain, Track.Url);

                    if (loadResult.LoadType is LavalinkLoadResultType.Error or LavalinkLoadResultType.Empty)
                    {
                        MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue = MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Remove(x => x.UUID, Track);
                        continue;
                    }

                    var loadedTrack = loadResult.LoadType switch
                    {
                        LavalinkLoadResultType.Track => loadResult.GetResultAs<LavalinkTrack>(),
                        LavalinkLoadResultType.Playlist => loadResult.GetResultAs<LavalinkPlaylist>().Tracks.First(),
                        LavalinkLoadResultType.Search => loadResult.GetResultAs<List<LavalinkTrack>>().First(),
                        _ => throw new InvalidOperationException("Unexpected load result type.")
                    };

                    guildPlayer = session.GetGuildPlayer(this.Guild) ?? throw new NullReferenceException();
                    this.ChannelId = guildPlayer.Channel.Id;

                    if (guildPlayer is not null)
                    {
                        _ = await guildPlayer.PlayAsync(loadedTrack);
                    }
                    else
                    {
                        this.Dispose(_bot, this.Guild.Id, "guildConnection is null");
                        continue;
                    }

                    if (!MusicPlugin.Plugin.Guilds![this.Guild.Id].Repeat)
                        MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue = MusicPlugin.Plugin.Guilds![this.Guild.Id].SongQueue.Remove(x => x.UUID, Track);
                }
            }
            catch (Exception ex)
            {
                MusicPlugin.Plugin!._logger.LogError(ex, "An exception occurred while trying to handle music Channel");

                _ = guildPlayer.DisconnectAsync();
                this.Dispose(_bot, this.Guild.Id, "Exception");
                throw;
            }
        }).Add(_bot);
    }
}
