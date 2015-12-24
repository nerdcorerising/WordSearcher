This is a project to work on managed perf. It solves a word search (i.e. a 2 dimensional array of characters) by looking for any words that can be made in a straight line.

I created it to use managed profiling to see how much I could tune the naive implementation. I may work on getting the algorithm better in the future, but right now it's written so it allocates as little as possible and avoids function calls/loops except where necessary.

It started out on my slow laptop running in about 400 seconds (single threaded) and is down to about 15 seconds (multi threaded). It may be even faster with a better algorithm, but I haven't convinced myself a better one exists.
