namespace Configurator.Presentation;

public interface IOperationGenerationAllocator
{
    bool TryAllocate(out OperationGeneration generation);
}
