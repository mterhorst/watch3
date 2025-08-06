using Microsoft.AspNetCore.SignalR;

namespace Watch3
{
    public class SignalingHub : Hub
    {
        public async Task JoinRoom(string room)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
        }

        public Task SendAnswer(string room, string answerSdp)
        {
            if (Routes.AnswerWaiters.TryGetValue(room, out var tcs))
            {
                tcs.TrySetResult(answerSdp);
            }
            return Task.CompletedTask;
        }
    }
}
