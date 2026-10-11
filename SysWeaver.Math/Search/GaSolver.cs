using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SysWeaver.Search
{


    /// <summary>
    /// An optimizer (searching for an optimal value) using genetic algorithms, can run in parallel on all cores
    /// </summary>
    public static class GaSolver
    {

        sealed class Population<T> : IComparable<Population<T>> where T : class
        {
#if DEBUG
            public override string ToString() => Error.ToString();
#endif//DEBUG

            public Population(Random rng)
            {
                var r = new Random(rng.Next());
                Rng = r;
                var c = rng.Next(11);
                while (c > 0)
                {
                    --c;
                    r.Next();
                }
            }
            public T Data;
            public double Error;
            public readonly Random Rng;

            public int CompareTo(Population<T> other) => Error.CompareTo(other.Error);
        }


        static readonly ParallelOptions Opt = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };


        static readonly Action<int, Action<int>> RunSerial = (count, action) =>
        {
            while (count > 0)
            {
                --count;
                action(count);
            }
        };

        static readonly Action<int, Action<int>> RunParallel = (count, action) =>
            Parallel.For(0, count, Opt, action);

        /// <summary>
        /// Solve or optimize a problem.
        /// The population is initialized with clones of <paramref name="data"/> (all but the first are mutated). Each generation the population is sorted by error,
        /// <see cref="IGaOperators{T}.Abort"/> is called, and all but the best states are replaced by mutated states
        /// (every second one is a mutated clone of one of the best states, the others mutate their own state further).
        /// The number of kept (best) states is populationSize / 32 rounded down to a power of two, at least 1 and at most 32, so the best state is never lost.
        /// </summary>
        /// <typeparam name="T">The state/data of the problem</typeparam>
        /// <param name="data">The initial state, after completion this is the most optimal state found (one of the states created by <see cref="IGaOperators{T}.Clone"/>)</param>
        /// <param name="rng">The rng to use, if null a new Random(43) is used</param>
        /// <param name="ops">Implements the required operations on a state</param>
        /// <param name="mutationRate">Maximum number of mutations to apply at generation 0 (at least 1), mutation rate goes down linearly to 1 at generation maxGenerations / 8 (or at generation mutationRate if maxGenerations is zero or less)</param>
        /// <param name="maxGenerations">Maximum number of generations to run the optimizations, zero or less for no limit (then <see cref="IGaOperators{T}.Abort"/> must return true at some point or the method never returns)</param>
        /// <param name="populationSize">The population size (with fewer than 64 states only the single best state is kept)</param>
        /// <param name="runInParallel">True to run in paralell (defaults to true in release builds and false in debug builds), the operators must then be thread safe</param>
        /// <returns>The result/stats of the solver</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ops"/> is null (DEBUG builds only).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="populationSize"/> is zero or negative (DEBUG builds only).</exception>
#if DEBUG
        public static GaResult SolveOrOptimize<T>(ref T data, Random rng, IGaOperators<T> ops, int mutationRate = 50, int maxGenerations = 0, int populationSize = 1024, bool runInParallel = false) where T : class
#else//DEBUG
        public static GaResult SolveOrOptimize<T>(ref T data, Random rng, IGaOperators<T> ops, int mutationRate = 50, int maxGenerations = 0, int populationSize = 1024, bool runInParallel = true) where T : class
#endif//DEBUG
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(ops);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(populationSize);
#endif//DEBUG
            rng = rng ?? new Random(43);

            if (mutationRate < 1)
                mutationRate = 1;
            Population<T>[] pop = new Population<T>[populationSize];
            for (int i = 0; i < populationSize; ++i)
                pop[i] = new Population<T>(rng);


            void mutate(T x, Random r)
            {
                var mc = r.Next(mutationRate);
                for (int m = 0; m <= mc; ++m)
                    ops.Mutate(x, r);
                ops.MutateLast(x, r);
            };



            var tdata = data;
            var opt = Opt;

            var run = runInParallel ? RunParallel : RunSerial;

            run(populationSize, i =>
            {
                var c = ops.Clone(tdata);
                var gene = pop[i];
                if (i > 0)
                    mutate(c, gene.Rng);
                gene.Data = c;
                gene.Error = ops.Error(c);
            });


            var maxRate = mutationRate;
            var minRateAt = maxGenerations <= 0 ? maxRate : (maxGenerations >> 3);
            if (minRateAt <= 0)
                minRateAt = 1;

            // Keep the best states: populationSize / 32 rounded down to a power of two, at least 1 and at most 32 (so that keepMask works)
            var keep = 1 << BitOperations.Log2((uint)(populationSize >> 5));
            if (keep > 32)
                keep = 32;
            var mutateSize = populationSize - keep;

            var keepMask = keep - 1;

            double lastMinErr = double.MaxValue;
            int lastMinGen = 0;

            int gen;
            for (gen = 0; (maxGenerations <= 0) || (gen < maxGenerations); ++gen)
            {
                Array.Sort(pop);
                foreach (var p in pop)
                    if ((rng.Next() & 1) == 0)
                        p.Rng.Next();

                mutationRate = maxRate - (gen * maxRate / minRateAt);
                if (mutationRate < 1)
                    mutationRate = 1;

                var minErr = pop[0].Error;
                if (minErr < lastMinErr)
                {
                    lastMinErr = minErr;
                    lastMinGen = gen;
                }
                var unchangedGens = gen - lastMinGen;
                if (ops.Abort(minErr, gen, unchangedGens, mutationRate))
                {
                    data = pop[0].Data;
                    return new GaResult(minErr, gen, unchangedGens);
                }

                run(mutateSize, i =>
                {
                    var srcIndex = i & keepMask;
                    var destIndex = keep + i;
                    var src = pop[srcIndex];
                    var dest = pop[destIndex];
                    var c = dest.Data;
                    if ((destIndex & 1) == 0)
                        c = ops.Clone(src.Data, c);
                    mutate(c, dest.Rng);
                    dest.Data = c;
                    dest.Error = ops.Error(c);
                });
            }
            Array.Sort(pop);
            var gdl = pop[0];
            data = gdl.Data;
            return new GaResult(gdl.Error, gen, gen - lastMinGen);

        }
    }
}
