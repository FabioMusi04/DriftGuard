# Dangerous child introduced via type drift

Changing `logging` from a scalar into an object with debug enabled must
still trigger the `$.logging.debug` rule. Expected exit code 1.
