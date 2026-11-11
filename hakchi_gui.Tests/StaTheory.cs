using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace hakchi_gui.Tests
{
    /// <summary>[Theory] execute sur un thread STA (WinForms l'exige pour creer des fenetres).</summary>
    [XunitTestCaseDiscoverer("hakchi_gui.Tests.StaTheoryDiscoverer", "hakchi_gui.Tests")]
    public sealed class StaTheoryAttribute : TheoryAttribute { }

    /// <summary>[Fact] execute sur un thread STA.</summary>
    [XunitTestCaseDiscoverer("hakchi_gui.Tests.StaFactDiscoverer", "hakchi_gui.Tests")]
    public sealed class StaFactAttribute : FactAttribute { }

    /// <summary>
    /// Levee par un test [StaFact] / [StaTheory] qui ne peut pas tourner sur ce poste (ex. jeux d'exemple absents) :
    /// le test est rapporte « ignore » avec ce motif, pas « echoue ».
    /// </summary>
    public sealed class SkipTestException : Exception
    {
        public SkipTestException(string reason) : base(reason) { }
    }

    /// <summary>Transforme l'echec d'un test du a <see cref="SkipTestException"/> en test ignore.</summary>
    internal sealed class SkippingMessageBus : IMessageBus
    {
        private readonly IMessageBus inner;
        public int Skipped;

        public SkippingMessageBus(IMessageBus inner) { this.inner = inner; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            var failed = message as ITestFailed;
            if (failed != null && failed.ExceptionTypes != null && failed.ExceptionTypes.Length > 0
                && failed.ExceptionTypes[0] == typeof(SkipTestException).FullName)
            {
                Skipped++;
                return inner.QueueMessage(new TestSkipped(failed.Test, failed.Messages[0]));
            }
            return inner.QueueMessage(message);
        }

        public void Dispose() { }

        public static async Task<RunSummary> Run(IMessageBus bus, Func<IMessageBus, Task<RunSummary>> body)
        {
            var skipping = new SkippingMessageBus(bus);
            var summary = await body(skipping);
            if (skipping.Skipped > 0)
            {
                summary.Failed -= skipping.Skipped;
                summary.Skipped += skipping.Skipped;
            }
            return summary;
        }
    }

    internal static class Sta
    {
        public static Task<RunSummary> Run(Func<Task<RunSummary>> body)
        {
            var tcs = new TaskCompletionSource<RunSummary>();
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(body().GetAwaiter().GetResult()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }
    }

    public sealed class StaTestCase : XunitTestCase
    {
        [Obsolete("Called by the de-serializer", true)]
        public StaTestCase() { }

        public StaTestCase(IMessageSink sink, TestMethodDisplay display, TestMethodDisplayOptions options, ITestMethod method, object[] args = null)
            : base(sink, display, options, method, args) { }

        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            => Sta.Run(() => SkippingMessageBus.Run(messageBus, bus => base.RunAsync(diagnosticMessageSink, bus, constructorArguments, aggregator, cancellationTokenSource)));
    }

    public sealed class StaTheoryTestCase : XunitTheoryTestCase
    {
        [Obsolete("Called by the de-serializer", true)]
        public StaTheoryTestCase() { }

        public StaTheoryTestCase(IMessageSink sink, TestMethodDisplay display, TestMethodDisplayOptions options, ITestMethod method)
            : base(sink, display, options, method) { }

        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            => Sta.Run(() => SkippingMessageBus.Run(messageBus, bus => base.RunAsync(diagnosticMessageSink, bus, constructorArguments, aggregator, cancellationTokenSource)));
    }

    public sealed class StaTheoryDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink sink;
        private readonly TheoryDiscoverer inner;

        public StaTheoryDiscoverer(IMessageSink diagnosticMessageSink)
        {
            sink = diagnosticMessageSink;
            inner = new TheoryDiscoverer(diagnosticMessageSink);
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            var display = discoveryOptions.MethodDisplayOrDefault();
            var options = discoveryOptions.MethodDisplayOptionsOrDefault();
            foreach (var tc in inner.Discover(discoveryOptions, testMethod, factAttribute).ToList())
            {
                if (tc is XunitTheoryTestCase)
                    yield return new StaTheoryTestCase(sink, display, options, testMethod);
                else
                    yield return new StaTestCase(sink, display, options, testMethod, tc.TestMethodArguments);
            }
        }
    }

    public sealed class StaFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink sink;

        public StaFactDiscoverer(IMessageSink diagnosticMessageSink) { sink = diagnosticMessageSink; }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            yield return new StaTestCase(sink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod);
        }
    }
}
