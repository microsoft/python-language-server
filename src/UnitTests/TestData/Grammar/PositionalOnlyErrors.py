def f(a, *b, /):
    pass

def f(a, *, b, /):
    pass

def f(a, *, b, **kwargs, /):
    pass

def f(a, **kwargs, /):
    pass

def f(a, /, b, /, c):
    pass
