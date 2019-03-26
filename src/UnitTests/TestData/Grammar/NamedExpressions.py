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

def foo(answer: (p := 42) = 5):
    return 1

lambda: (y := 1)
