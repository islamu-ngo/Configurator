namespace Configurator.Presentation;

public sealed class OperationGenerationAllocator : IOperationGenerationAllocator
{
    private long _current;

    public bool TryAllocate(out OperationGeneration generation)
    {
        while (true)
        {
            long current = Volatile.Read(ref _current);
            if (current == long.MaxValue)
            {
                generation = default;
                return false;
            }

            long next = current + 1;
            if (Interlocked.CompareExchange(ref _current, next, current) == current)
            {
                generation = new OperationGeneration(next);
                return true;
            }
        }
    }
}
