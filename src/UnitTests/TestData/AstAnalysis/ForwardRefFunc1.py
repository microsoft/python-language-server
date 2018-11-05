class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.funcA()

    def funcA(self):
        return A()
