namespace MedBench.Core.Interfaces;

public interface IModelRunnerFactory
{
    IModelRunner CreateModelRunner(Model model);
} 