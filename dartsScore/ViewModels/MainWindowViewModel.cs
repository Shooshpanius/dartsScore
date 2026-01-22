using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace dartsScore.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        /// <summary>
        /// ViewModel главного окна приложения.
        /// Содержит:
        /// - список сохранённых игроков (Players),
        /// - участников текущей игры (Participants),
        /// - заголовки раундов (RoundHeaders),
        /// - логику управления ходами и раундами (ThrowsLeft, CurrentRoundIndex и т.д.).
        /// </summary>

        public MainWindowViewModel()
        {
            Players = new ObservableCollection<PlayerEntry>();
            AddPlayerCommand = new RelayCommand(AddPlayer);
            RemoveSelectedPlayerCommand = new RelayCommand(RemoveSelectedPlayer);

            Participants = new ObservableCollection<PlayerInfo>();
            // maintain HasParticipants flag for UI
            HasParticipants = Participants.Count > 0;
            Participants.CollectionChanged += (s, e) => { HasParticipants = Participants.Count > 0; };

            AddToGameCommand = new RelayCommand<string>(AddToGame);
            RemoveParticipantCommand = new RelayCommand<PlayerInfo>(RemoveParticipant);

            // load persisted players
            LoadPlayers();

            // ensure per-row AddToGameCommand is assigned for entries loaded from storage
            foreach (var entry in Players)
            {
                entry.AddToGameCommand = new RelayCommand<string?>(AddToGameWrapper, _ => true) as System.Windows.Input.ICommand;
            }

            // persist on collection changes
            Players.CollectionChanged += Players_CollectionChanged;
            Players.CollectionChanged += (s, e) =>
            {
                // set AddToGameCommand on each entry so row template can bind directly
                foreach (var entry in Players)
                {
                    entry.AddToGameCommand = new RelayCommand<string?>(AddToGameWrapper, _ => true) as System.Windows.Input.ICommand;
                }
            };

            // ensure at least one round header exists
            EnsureRoundCapacityForAll(1);
        }

        public string Greeting { get; } = "Welcome to Avalonia!";

        public ObservableCollection<PlayerEntry> Players { get; }

        private string _newPlayerName = string.Empty;
        public string NewPlayerName
        {
            get => _newPlayerName;
            set => SetProperty(ref _newPlayerName, value);
        }

        // Selected player name (used by other parts of code)
        private string _selectedPlayer = string.Empty;
        public string SelectedPlayer
        {
            get => _selectedPlayer;
            set
            {
                if (SetProperty(ref _selectedPlayer, value))
                {
                    // persist selected player change
                    SavePlayers();
                    // also notify that score property may change
                    OnPropertyChanged(nameof(SelectedPlayerScore));
                }
            }
        }

        // Selected player entry (for settings ListBox selection)
        private PlayerEntry? _selectedPlayerEntry;
        public PlayerEntry? SelectedPlayerEntry
        {
            get => _selectedPlayerEntry;
            set
            {
                if (SetProperty(ref _selectedPlayerEntry, value))
                {
                    // keep SelectedPlayer (string) in sync for other logic and persistence
                    SelectedPlayer = value?.Name ?? string.Empty;
                }
            }
        }

        public ICommand AddPlayerCommand { get; }
        public ICommand RemoveSelectedPlayerCommand { get; }
        public ICommand AddToGameCommand { get; }
        public ICommand RemoveParticipantCommand { get; }

        // wrapper to match PlayerEntry.AddToGameCommand signature
        private void AddToGameWrapper(string? name)
        {
            AddToGame(name);
        }

        public ObservableCollection<PlayerInfo> Participants { get; }
        private bool _hasParticipants;
        public bool HasParticipants { get => _hasParticipants; set { if (SetProperty(ref _hasParticipants, value)) OnPropertyChanged(nameof(HasParticipants)); } }
        public ObservableCollection<RoundHeader> RoundHeaders { get; } = new ObservableCollection<RoundHeader>();
        private int _currentRoundIndex = 0;
        public int CurrentRoundIndex
        {
            get => _currentRoundIndex;
            private set
            {
                if (SetProperty(ref _currentRoundIndex, value))
                    UpdateCurrentRoundFlags();
            }
        }
        private int _throwsThisRound = 0;

        private PlayerInfo? _selectedParticipant;
        public PlayerInfo? SelectedParticipant
        {
            get => _selectedParticipant;
            set
            {
                if (SetProperty(ref _selectedParticipant, value))
                {
                    // when selected participant changes, also set SelectedPlayer to match
                    SelectedPlayer = value?.Name ?? string.Empty;

                    // synchronize ActiveParticipantIndex with the selected participant
                    if (value != null)
                    {
                        var idx = Participants.IndexOf(value);
                        if (idx >= 0)
                        {
                            ActiveParticipantIndex = idx;
                            // if throws left not initialized, set to default
                            if (ThrowsLeft <= 0)
                                ThrowsLeft = ThrowsPerTurn;
                            OnPropertyChanged(nameof(ActiveParticipantName));
                            OnPropertyChanged(nameof(ThrowsLeft));
            UpdateActiveFlags();
                        }
                    }
                    else
                    {
                        ActiveParticipantIndex = -1;
                        OnPropertyChanged(nameof(ActiveParticipantName));
                    }
                }
            }
        }

        private void AddPlayer()
        {
            if (string.IsNullOrWhiteSpace(NewPlayerName))
                return;
            // Добавляем нового игрока в список сохранённых игроков (на вкладке "Настройки").
            var entry = new PlayerEntry { Name = NewPlayerName.Trim() };
            Players.Add(entry);
            NewPlayerName = string.Empty;
        }

        private void RemoveSelectedPlayer()
        {
            if (SelectedPlayerEntry == null)
                return;

            Players.Remove(SelectedPlayerEntry);
            SelectedPlayerEntry = null;
            SelectedPlayer = string.Empty;
        }

        /// <summary>
        /// Добавляет игрока по имени в текущую игровую сессию (Participants).
        /// Если игрок отсутствует в списке настроек, он будет туда добавлен.
        /// </summary>
        /// <param name="player">Имя игрока.</param>
        private void AddToGame(string? player)
        {
            if (string.IsNullOrWhiteSpace(player))
                return;
            if (Participants.Any(p => p.Name == player))
                return;
            var info = new PlayerInfo(player, GetPlayerScore(player));
            Participants.Add(info);

            // ensure active participant initialized
            EnsureActiveParticipant();
            EnsureRoundCapacityForAll(CurrentRoundIndex + 1);
            // mark player entry as in-game
            var entry = Players.FirstOrDefault(x => x.Name == player);
            if (entry != null) entry.InGame = true;
            else
            {
                // if player not in settings list, add them and mark in-game
                var newEntry = new PlayerEntry { Name = player, InGame = true };
                Players.Add(newEntry);
            }
        }

        private void EnsureRoundCapacityForAll(int rounds)
        {
            if (rounds <= 0) rounds = 1;
            // ensure headers
            while (RoundHeaders.Count < rounds) RoundHeaders.Add(new RoundHeader { Number = RoundHeaders.Count + 1 });

            foreach (var p in Participants)
            {
                while (p.RoundScores.Count < rounds) p.RoundScores.Add(new RoundScoreEntry { Value = 0, Parent = p, Index = p.RoundScores.Count, IsFuture = (p.RoundScores.Count > CurrentRoundIndex) });
            }

            UpdateCurrentRoundFlags();
        }

        public void RemoveParticipant(PlayerInfo? p)
        {
            if (p == null) return;
            Participants.Remove(p);
            // unmark in Players list
            var entry = Players.FirstOrDefault(x => x.Name == p.Name);
            if (entry != null) entry.InGame = false;
            if (Participants.Count == 0)
            {
                ActiveParticipantIndex = -1;
                ThrowsLeft = 0;
                OnPropertyChanged(nameof(ActiveParticipantName));
                OnPropertyChanged(nameof(ThrowsLeft));
            }
        }

        // Turn management
        public int ThrowsPerTurn { get; } = 3;

        public int ThrowsLeft { get; private set; } = 0;

        public int ActiveParticipantIndex { get; private set; } = -1;

        public string ActiveParticipantName => (ActiveParticipantIndex >= 0 && ActiveParticipantIndex < Participants.Count) ? Participants[ActiveParticipantIndex].Name : string.Empty;

        private void EnsureActiveParticipant()
        {
            if (ActiveParticipantIndex < 0 && Participants.Count > 0)
            {
                ActiveParticipantIndex = 0;
                ThrowsLeft = ThrowsPerTurn;
                SelectedParticipant = Participants[0];
                SelectedPlayer = Participants[0].Name;
                OnPropertyChanged(nameof(ActiveParticipantName));
                OnPropertyChanged(nameof(ThrowsLeft));
                UpdateActiveFlags();
            }
        }

        private void UpdateActiveFlags()
        {
            for (int i = 0; i < Participants.Count; i++)
            {
                Participants[i].IsActive = (i == ActiveParticipantIndex);
            }
            UpdateCurrentRoundFlags();
        }

        private void UpdateCurrentRoundFlags()
        {
            for (int i = 0; i < RoundHeaders.Count; i++)
            {
                RoundHeaders[i].IsCurrent = (i == CurrentRoundIndex);
                RoundHeaders[i].IsPast = (i < CurrentRoundIndex);
            }
            // update participant round entry flags
            for (int p = 0; p < Participants.Count; p++)
            {
                var scores = Participants[p].RoundScores;
                for (int i = 0; i < scores.Count; i++)
                {
                    // mark current column
                    scores[i].IsCurrent = (i == CurrentRoundIndex);
                    // determine whether this round is in the future (no overall total should be shown)
                    // Show total for rounds strictly before current, or for current round only after there's a value
                    bool isFuture = !(i < CurrentRoundIndex || (i == CurrentRoundIndex && scores[i].Value > 0));
                    scores[i].IsFuture = isFuture;
                    scores[i].UpdateDisplayTotal();
                    // highlight only the active participant's current-round cell
                    scores[i].IsActiveCell = (p == ActiveParticipantIndex && i == CurrentRoundIndex);
                }
            }
        }

        public (int PrevIndex, int PrevThrowsLeft, int PrevRoundIndex, int PrevThrowsThisRound) RecordThrow(string player, int points)
        {
            var prev = (PrevIndex: ActiveParticipantIndex, PrevThrowsLeft: ThrowsLeft, PrevRoundIndex: CurrentRoundIndex, PrevThrowsThisRound: _throwsThisRound);
            // apply score and record history
            AdjustScore(player, points);

            // if active participant, accumulate round score and count throws in round
            if (!string.IsNullOrEmpty(ActiveParticipantName) && player == ActiveParticipantName && ActiveParticipantIndex >= 0 && ActiveParticipantIndex < Participants.Count)
            {
                var active = Participants[ActiveParticipantIndex];
                EnsureRoundCapacityForAll(CurrentRoundIndex + 1);
                var entry = active.RoundScores[CurrentRoundIndex];
                entry.Value += points;
                entry.IsFuture = false;
                // also update RoundScore convenience property
                active.RoundScore = active.RoundScores[CurrentRoundIndex].Value;
                _throwsThisRound++;
            }

            // if throw belongs to active participant, decrement throws
            if (!string.IsNullOrEmpty(ActiveParticipantName) && player == ActiveParticipantName)
            {
                if (ThrowsLeft > 0) ThrowsLeft--;
                OnPropertyChanged(nameof(ThrowsLeft));

                // Advance to next participant when ThrowsLeft reaches 0
                if (ThrowsLeft <= 0 && Participants.Count > 0)
                {
                    // advance to next participant
                    ActiveParticipantIndex = (ActiveParticipantIndex + 1) % Participants.Count;
                    ThrowsLeft = ThrowsPerTurn;
                    SelectedParticipant = Participants[ActiveParticipantIndex];
                    SelectedPlayer = Participants[ActiveParticipantIndex].Name;
                    OnPropertyChanged(nameof(ActiveParticipantName));
                    OnPropertyChanged(nameof(ThrowsLeft));
                    UpdateActiveFlags();
                    // if completed a full round (each participant had ThrowsPerTurn throws), advance current round
                    if (_throwsThisRound >= Participants.Count * ThrowsPerTurn)
                    {
                        _throwsThisRound = 0;
                CurrentRoundIndex++;
                EnsureRoundCapacityForAll(CurrentRoundIndex + 1);
                    }
                }
            }

            return prev;
        }

        public void UndoThrow(int prevIndex, int prevThrowsLeft, int prevRoundIndex, int prevThrowsThisRound, string player, int points)
        {
            // revert score
            AdjustScore(player, -points);

            // restore turn state
            ActiveParticipantIndex = prevIndex;
            ThrowsLeft = prevThrowsLeft;
            // restore round state
            CurrentRoundIndex = prevRoundIndex;
            _throwsThisRound = prevThrowsThisRound;
            if (ActiveParticipantIndex >= 0 && ActiveParticipantIndex < Participants.Count)
            {
                SelectedParticipant = Participants[ActiveParticipantIndex];
                SelectedPlayer = Participants[ActiveParticipantIndex].Name;
            }
            // revert round score as well if in range
            if (CurrentRoundIndex >= 0 && CurrentRoundIndex < 1000)
            {
                var part = Participants.FirstOrDefault(p => p.Name == player);
                if (part != null && part.RoundScores.Count > CurrentRoundIndex)
                {
                    var entry = part.RoundScores[CurrentRoundIndex];
                    entry.Value -= points;
                    if (entry.Value < 0) entry.Value = 0;
                    OnPropertyChanged(nameof(Participants));
                }
            }
            OnPropertyChanged(nameof(ActiveParticipantName));
            OnPropertyChanged(nameof(ThrowsLeft));
            UpdateActiveFlags();
        }

        public void AdvanceTurnManually()
        {
            if (Participants.Count == 0) return;

            // If current active participant has unfinished throws, count them as throws with 0 points
            if (ActiveParticipantIndex >= 0 && ActiveParticipantIndex < Participants.Count)
            {
                int remaining = ThrowsLeft;
                if (remaining > 0)
                {
                    // mark current round cell as no longer future (even if value stays the same)
                    var active = Participants[ActiveParticipantIndex];
                    EnsureRoundCapacityForAll(CurrentRoundIndex + 1);
                    if (active.RoundScores.Count > CurrentRoundIndex)
                    {
                        var entry = active.RoundScores[CurrentRoundIndex];
                        entry.IsFuture = false;
                        // Round score unchanged for zero-point throws
                        active.RoundScore = entry.Value;
                    }

                    // count the remaining throws as performed (with 0 points)
                    _throwsThisRound += remaining;
                    ThrowsLeft = 0;
                    OnPropertyChanged(nameof(ThrowsLeft));
                }
            }

            // advance to next participant (same logic as when ThrowsLeft reaches 0 after real throws)
            ActiveParticipantIndex = (ActiveParticipantIndex + 1) % Participants.Count;
            ThrowsLeft = ThrowsPerTurn;
            SelectedParticipant = Participants[ActiveParticipantIndex];
            SelectedPlayer = Participants[ActiveParticipantIndex].Name;
            OnPropertyChanged(nameof(ActiveParticipantName));
            OnPropertyChanged(nameof(ThrowsLeft));
            UpdateActiveFlags();

            // if completed a full round (each participant had ThrowsPerTurn throws), advance current round
            if (_throwsThisRound >= Participants.Count * ThrowsPerTurn)
            {
                _throwsThisRound = 0;
                CurrentRoundIndex++;
                EnsureRoundCapacityForAll(CurrentRoundIndex + 1);
            }
        }

        private void Players_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SavePlayers();
        }

        private static string GetStoragePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dartsScore");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "players.json");
        }
        // PlayerEntry defined below as observable type

        private void LoadPlayers()
        {
            try
            {
                var path = GetStoragePath();
                if (!File.Exists(path))
                    return;

                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<PlayersDto>(json);
                if (dto == null)
                    return;

                Players.Clear();
                foreach (var p in dto.Players ?? new System.Collections.Generic.List<string>())
                    Players.Add(new PlayerEntry { Name = p, InGame = false });

                if (!string.IsNullOrEmpty(dto.Selected))
                {
                    var sel = Players.FirstOrDefault(x => x.Name == dto.Selected);
                    if (sel != null) SelectedPlayer = sel.Name ?? string.Empty;
                }
            }
            catch
            {
                // ignore load errors
            }
        }

        private void SavePlayers()
        {
            try
            {
                var path = GetStoragePath();
                var dto = new PlayersDto { Players = new System.Collections.Generic.List<string>(Players.Select(p => p.Name ?? string.Empty)), Selected = SelectedPlayer };
                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // ignore save errors
            }
        }

        private class PlayersDto
        {
            public System.Collections.Generic.List<string>? Players { get; set; }
            public string? Selected { get; set; }
        }

        public class PlayerEntry : ObservableObject
        {
            private string? _name;
            public string? Name { get => _name; set => SetProperty(ref _name, value); }

            private bool _inGame;
            public bool InGame { get => _inGame; set => SetProperty(ref _inGame, value); }
            // Row-level command to add this player to the game (set by ViewModel)
            public System.Windows.Input.ICommand? AddToGameCommand { get; set; }
        }

        public class PlayerInfo : ObservableObject
        {
            public PlayerInfo(string name, int score = 0)
            {
                Name = name;
                _score = score;
                _roundScore = 0;
                _isActive = false;
                _isHighlighted = false;
                // initialize per-round scores (support up to 20 rounds initially)
                RoundScores = new ObservableCollection<RoundScoreEntry>(Enumerable.Range(0,20).Select((_, idx) => new RoundScoreEntry { Value = 0, Parent = this, Index = idx, IsFuture = true }).ToList());
            }

            public string Name { get; }

            private int _score;
            public int Score
            {
                get => _score;
                set => SetProperty(ref _score, value);
            }

            private int _roundScore;
            public int RoundScore
            {
                get => _roundScore;
                set => SetProperty(ref _roundScore, value);
            }

            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                set
                {
                    if (SetProperty(ref _isActive, value))
                        OnPropertyChanged(nameof(HighlightState));
                }
            }

            private bool _isHighlighted;
            public bool IsHighlighted
            {
                get => _isHighlighted;
                set
                {
                    if (SetProperty(ref _isHighlighted, value))
                        OnPropertyChanged(nameof(HighlightState));
                }
            }

            public bool HighlightState => IsActive || IsHighlighted;

            public ObservableCollection<RoundScoreEntry> RoundScores { get; }
        }

        public class RoundHeader : ObservableObject
        {
            private bool _isCurrent;
            private bool _isPast;
            public int Number { get; set; }
            public bool IsCurrent { get => _isCurrent; set { if (SetProperty(ref _isCurrent, value)) OnPropertyChanged(nameof(BackgroundBrush)); } }
            public bool IsPast { get => _isPast; set { if (SetProperty(ref _isPast, value)) OnPropertyChanged(nameof(BackgroundBrush)); } }

            public Avalonia.Media.IBrush BackgroundBrush
            {
                get
                {
                    if (IsCurrent)
                        return Avalonia.Media.Brushes.LightGreen;
                    if (IsPast)
                        return Avalonia.Media.Brushes.LightGray;
                    return Avalonia.Media.Brushes.Transparent;
                }
            }
        }

        public class RoundScoreEntry : ObservableObject
        {
            private int _value;
            public int Value { get => _value; set { if (SetProperty(ref _value, value)) { UpdateDisplayTotal(); if (Parent != null) { for (int i = Index; i < Parent.RoundScores.Count; i++) Parent.RoundScores[i].UpdateDisplayTotal(); } } } }
            private bool _isCurrent;
            public bool IsCurrent { get => _isCurrent; set { if (SetProperty(ref _isCurrent, value)) UpdateBackground(); } }
            public PlayerInfo? Parent { get; set; }
            public int Index { get; set; }
            private bool _isFuture;
            public bool IsFuture { get => _isFuture; set { if (SetProperty(ref _isFuture, value)) { OnPropertyChanged(nameof(DisplayTotal)); UpdateBackground(); } } }

            public string DisplayTotal
            {
                get
                {
                    if (Parent == null || IsFuture) return string.Empty;
                    // cumulative total up to this round (inclusive)
                    int sum = 0;
                    for (int i = 0; i <= Index && i < Parent.RoundScores.Count; i++)
                        sum += Parent.RoundScores[i].Value;
                    return sum.ToString();
                }
            }
            public void UpdateDisplayTotal() => OnPropertyChanged(nameof(DisplayTotal));
            private bool _isActiveCell;
            public bool IsActiveCell { get => _isActiveCell; set { if (SetProperty(ref _isActiveCell, value)) UpdateBackground(); } }

            public Avalonia.Media.IBrush BackgroundBrush
            {
                get
                {
                    if (IsActiveCell)
                        return Avalonia.Media.Brushes.LightGreen;
                    if (IsCurrent)
                        return Avalonia.Media.Brushes.LightGoldenrodYellow;
                    if (!IsFuture)
                        return Avalonia.Media.Brushes.LightGray;
                    return Avalonia.Media.Brushes.Transparent;
                }
            }

            public void UpdateBackground()
            {
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        // Simple score storage per player (name -> total points)
        private readonly System.Collections.Generic.Dictionary<string, int> _scores = new();

        public int GetPlayerScore(string player)
        {
            if (player == null) return 0;
            return _scores.TryGetValue(player, out var v) ? v : 0;
        }

        public (int PrevIndex, int PrevThrowsLeft, int PrevRoundIndex, int PrevThrowsThisRound) AddScoreToPlayer(string player, int points)
        {
            /// <summary>
            /// Добавляет очки игроку и возвращает предыдущее состояние (для возможности Undo).
            /// Возвращает кортеж с информацией о предыдущем состоянии очереди и раунда.
            /// </summary>
            return RecordThrow(player, points);
        }

        public void AdjustScore(string player, int delta)
        {
            if (string.IsNullOrEmpty(player)) return;
            if (!_scores.ContainsKey(player))
                _scores[player] = 0;
            _scores[player] += delta;

            // ensure not negative
            if (_scores[player] < 0) _scores[player] = 0;

            // notify that SelectedPlayerScore may have changed
            OnPropertyChanged(nameof(SelectedPlayerScore));

            // Also update participant entry if present
            // Обновляем представление участника, если он в текущей игре — синхронизируем общий счёт и раундовый счёт
            var part = Participants.FirstOrDefault(p => p.Name == player);
            if (part != null)
            {
                part.Score = _scores[player];
                // adjust round score as well
                part.RoundScore += delta;
                if (part.RoundScore < 0) part.RoundScore = 0;
            }
        }

        public int SelectedPlayerScore => GetPlayerScore(SelectedPlayer);
    }
}
