[x for x, y in (pairs2 := pairs) if x % 2 == 0]
[x for x, y in ([1, 2, 3, pairs2 := pairs]) if x % 2 == 0]
{x: y for x, y in (pairs2 := pairs) if x % 2 == 0}
{x for x, y in (pairs2 := pairs) if x % 2 == 0}
foo = (x for x, y in ([1, 2, 3, pairs2 := pairs]) if x % 2 == 0)

[[(j := j) for i in range(5)] for j in range(5)] # INVALID
[i := 0 for i, j in stuff]                       # INVALID
[i+1 for i in (i := stuff)]                      # INVALID

[False and (i := 0) for i, j in stuff]     # INVALID
[i for i, j in stuff if True or (j := 1)]  # INVALID

[i+1 for i in (j := stuff)]                    # INVALID
[i+1 for i in range(2) for j in (k := stuff)]  # INVALID
[i+1 for i in [j for j in (k := stuff)]]       # INVALID
[i+1 for i in (lambda: (j := stuff))()]        # INVALID

class Example:
    [(j := i) for i in range(5)]  # INVALID

[i for i in stuff if True or (j := 1) for j in range(10)]
