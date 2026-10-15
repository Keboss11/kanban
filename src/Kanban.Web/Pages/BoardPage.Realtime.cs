using Kanban.Shared.Boards;
using Kanban.Shared.Realtime;
using Kanban.Web.Components;
using Kanban.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Kanban.Web.Pages;

/// <summary>Tiempo real: cambios hechos en otras pestañas o por la IA llegan por SignalR.</summary>
public partial class BoardPage
{
    [Inject] private BoardRealtime Realtime { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private Toasts? _toasts;
    private Guid? _joinedBoard;
    private bool _reloading;
    private bool _reloadPending;

    protected override void OnInitialized()
    {
        Realtime.Changed += OnRemoteChangeAsync;
        Realtime.Deleted += OnRemoteDeleteAsync;
        Realtime.Resynced += OnResyncedAsync;
        Realtime.StateChanged += OnRealtimeStateChanged;
    }

    /// <summary>Sin conexión en tiempo real la página sigue funcionando; solo falta el indicador.</summary>
    private async Task JoinRealtimeAsync()
    {
        if (_joinedBoard == BoardId)
            return;
        try
        {
            await Realtime.JoinAsync(BoardId);
            _joinedBoard = BoardId;
        }
        catch (Exception)
        {
            _joinedBoard = null;
        }
        await InvokeAsync(StateHasChanged);
    }

    private Task OnRemoteChangeAsync(BoardChangedEvent change)
    {
        if (change.BoardId != BoardId)
            return Task.CompletedTask;

        return InvokeAsync(async () =>
        {
            var ai = change.Source == ActivitySourceDto.Ai;
            _toasts?.Show(ai ? $"🤖 Claude {change.Summary}" : $"{change.UserName} {change.Summary}", ai);
            await RefreshFromServerAsync();
        });
    }

    private Task OnRemoteDeleteAsync(Guid boardId)
    {
        if (boardId != BoardId)
            return Task.CompletedTask;
        return InvokeAsync(() => Nav.NavigateTo(""));
    }

    private Task OnResyncedAsync() => InvokeAsync(RefreshFromServerAsync);

    private void OnRealtimeStateChanged() => _ = InvokeAsync(StateHasChanged);

    /// <summary>
    /// Recarga el tablero. Si llegan varios avisos seguidos (p. ej. la IA creando tarjetas),
    /// se agrupan: como mucho una recarga en curso y otra pendiente.
    /// </summary>
    private async Task RefreshFromServerAsync()
    {
        if (_reloading)
        {
            _reloadPending = true;
            return;
        }

        _reloading = true;
        try
        {
            do
            {
                _reloadPending = false;
                await ReloadAsync();
                // La tarjeta abierta se actualiza o, si la han borrado, se cierra el modal.
                if (_openCard is not null)
                    _openCard = _board?.FindCard(_openCard.Id);
                if (_activity is not null)
                    await _activity.RefreshAsync();
                StateHasChanged();
            }
            while (_reloadPending);
        }
        finally
        {
            _reloading = false;
        }
    }

    private async Task DisposeRealtimeAsync()
    {
        Realtime.Changed -= OnRemoteChangeAsync;
        Realtime.Deleted -= OnRemoteDeleteAsync;
        Realtime.Resynced -= OnResyncedAsync;
        Realtime.StateChanged -= OnRealtimeStateChanged;
        if (_joinedBoard is { } board)
        {
            try
            {
                await Realtime.LeaveAsync(board);
            }
            catch (Exception)
            {
            }
        }
    }
}
