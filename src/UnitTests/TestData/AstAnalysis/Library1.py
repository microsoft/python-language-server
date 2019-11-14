class Foo():
	"""This is Foo and the docs should be here"""
    def __init__(self):
        self.x = 1234
        self.y = "str"


def make_foo():
	"""This is make_foo and the docs should be here"""
    return Foo()

def print_foo(f):
	"""This is print_foo and the docs should be here"""
    print(f"{f.x}, {f.y}")
