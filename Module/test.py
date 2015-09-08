#Init() is called in the begining. Here we do not have any data yet.
def Init():
    pass

#Execute() is called repeatedly each iteration to transform inputs to outputs.
#We can access a list of lists of floats called Input
#and a list of lists called Output.
#We should not change their dimensions!
def Execute():
	#iterate over all inputs and all their elements and sum them
    sum = 0
    for i in xrange(len(Input)):
        for j in xrange(len(Input[i])):
            sum += Input[i][j]

    for i in xrange(len(Output)):
        for j in xrange(len(Output[i])):
            Output[i][j] = sum