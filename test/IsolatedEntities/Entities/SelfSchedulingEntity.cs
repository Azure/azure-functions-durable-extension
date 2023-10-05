using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace IsolatedEntities
{
    public class SelfSchedulingEntity
    {
        public string Value { get; set; } = "";

        public void Start(TaskEntityContext context)
        {
            var now = DateTime.UtcNow;

            var timeA = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            var timeB = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            var timeC = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
            var timeD = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(4);

            context.SignalEntity(context.Id, nameof(D), options: timeD);
            context.SignalEntity(context.Id, nameof(C), options: timeC);
            context.SignalEntity(context.Id, nameof(B), options: timeB);
            context.SignalEntity(context.Id, nameof(A), options: timeA);
        }

        public void A()
        {
            this.Value += "A";
        }

        public Task B()
        {
            this.Value += "B";
            return Task.Delay(100);
        }

        public void C()
        {
            this.Value += "C";
        }

        public Task<int> D()
        {
            this.Value += "D";
            return Task.FromResult(111);
        }

        [Function(nameof(SelfSchedulingEntity))]
        public static Task SelfSchedulingEntityFunction([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync<SelfSchedulingEntity>();
        }
    }
}
