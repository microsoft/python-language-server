(a := 1)
[a := 1, 1]

def f(x):
    return 1

[y := f(1), y**2]

# Handle a matched regex
if (match := 1) is not None:
    pass

# A loop
while chunk := f(1):
   pass

def foo(answer: (p := 42) = 5, cat = ''):
    return 1

lambda: (y := 1)
x = (y := 1)
foo(x := 1, cat='vector')

# Precedence checks
(a := 1 and None)
# a is None

(a := 1 if False else 2)
# a is 2

((x) := 1)

if x := a:
    pass

class LambdaTop:
    [(lambda: (z := x)) for x in range(1)]

[(lambda x: (x := x)) for x in range(1)]
