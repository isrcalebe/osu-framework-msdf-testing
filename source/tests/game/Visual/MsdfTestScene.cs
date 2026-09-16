namespace Msdf.Game.Tests.Visual;

public abstract partial class MsdfTestScene : TestScene
{
    protected override ITestSceneTestRunner CreateRunner()
        => new MsdfTestRunner();

    private partial class MsdfTestRunner : MsdfGameBase, ITestSceneTestRunner
    {
        private TestSceneTestRunner.TestRunner? runner;

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            Add(runner = new TestSceneTestRunner.TestRunner());
        }

        public void RunTestBlocking(TestScene test)
            => runner?.RunTestBlocking(test);
    }
}
