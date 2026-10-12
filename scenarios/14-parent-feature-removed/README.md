# Parent feature removed

Removing the entire `features` object must still trigger the removal rule
for `$.features.billingEnabled`. Expected exit code 1 with the production policy.
