class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.func1()

    def func1(self):
        return self.func2()

    def func2(self):
        return self.func3()

    def func3(self):
        return A()
