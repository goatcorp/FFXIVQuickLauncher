using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.Util
{
    public class MultiDisposable : IDisposable
    {
        private readonly Stack<IDisposable> Disposables = new();

        public T With<T>(T disposable) where T : IDisposable
        {
            Disposables.Push(disposable);
            return disposable;
        }

        public void Dispose()
        {
            while (Disposables.Count > 0)
                Disposables.Pop().Dispose();
        }
    }
}
