using System;
using System.Threading;
using System.Threading.Tasks;
namespace BlackHoleBin {
    // Shell COM operations require an STA. Never occupy the animation/UI thread.
    internal static class OperationWorker {
        public static Task<T> Run<T>(Func<T> action) {
            var completion = new TaskCompletionSource<T>();
            var thread = new Thread(delegate() {
                try { completion.SetResult(action()); }
                catch(Exception ex) { completion.SetException(ex); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
