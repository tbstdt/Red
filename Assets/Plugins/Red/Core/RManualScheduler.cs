#if CSHARP_7_OR_LATER || (UNITY_2018_3_OR_NEWER && (NET_STANDARD_2_0 || NET_4_6))
namespace Red {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UniRx;
    using UnityEngine;

    public class RManualScheduler : IScheduler, ISchedulerQueueing {
        public DateTimeOffset Now => Scheduler.Now;
        protected readonly List<(DateTimeOffset time, Action action)> List
            = new List<(DateTimeOffset time, Action action)>();
        protected readonly List<(DateTimeOffset time, Action action)> RemoveList
            = new List<(DateTimeOffset time, Action action)>();

        protected readonly List<IHelper> Helpers     = new List<IHelper>();
        protected readonly List<IHelper> TempHelpers = new List<IHelper>();

        public IDisposable Schedule(Action action) {
            var temp = (DateTimeOffset.MinValue, action);
            this.List.Add(temp);
            return null;
        }

        public IDisposable Schedule(TimeSpan dueTime, Action action) {
            var time = Scheduler.Normalize(dueTime);
            var temp = (this.Now.Add(time), action);
            this.List.Add(temp);
            return null;
        }

        public virtual void Publish() {
            this.RemoveList.Clear();

            for (int i = 0; i < this.List.Count; i++) {
                var item = this.List[i];
                if (item.time <= this.Now) {
                    MainThreadDispatcher.UnsafeSend(item.action);
                    this.RemoveList.Add(item);
                }
            }

            this.RemoveList.ForEach(item => this.List.Remove(item));

            this.Helpers.ForEach(h => h.Publish());
            this.Helpers.AddRange(this.TempHelpers);
            this.TempHelpers.Clear();
        }

        public void ScheduleQueueing<T>(ICancelable cancel, T state, Action<T> action) {
            this.GetHelper<T>().Schedule(action, state);
        }

        protected Helper<T> GetHelper<T>() {
            var temp = this.Helpers.FirstOrDefault(h => h is T);
            if (temp == null) {
                temp = new Helper<T>();
                this.TempHelpers.Add(temp);
            }

            return (Helper<T>) temp;
        }

        protected interface IHelper {
            void Publish();
        }

        protected class Helper<T> : IHelper {
            private readonly List<(Action<T> action, T state)> list
                = new List<(Action<T> action, T state)>();

            public void Schedule(Action<T> action, T state) {
                this.list.Add((action, state));
            }

            public void Publish() {
                for (int i = 0; i < this.list.Count; i++) {
                    var (action, state) = this.list[i];
                    MainThreadDispatcher.UnsafeSend(action, state);
                }

                this.list.Clear();
            }
        }
    }

    public class RManualSchedulerLocked : RManualScheduler {
        private readonly List<(DateTimeOffset time, Action action)> temporaryList
            = new List<(DateTimeOffset time, Action action)>();
        
        public override void Publish() {
            this.RemoveList.Clear();
            
            this.temporaryList.Clear();
            this.temporaryList.AddRange(this.List);

            for (int i = 0; i < this.temporaryList.Count; i++) {
                var item = this.temporaryList[i];
                if (item.time <= this.Now) {
                    MainThreadDispatcher.UnsafeSend(item.action);
                    this.RemoveList.Add(item);
                }
            }

            this.RemoveList.ForEach(item => this.List.Remove(item));

            this.Helpers.ForEach(h => h.Publish());
            this.Helpers.AddRange(this.TempHelpers);
            this.TempHelpers.Clear();
        }
    }
}
#endif