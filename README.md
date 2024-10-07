# rep-esimport-proxy

## Project Overview

This is the rep-esimport-proxy solution. It includes:

- ClientTrackingSQSLambda - lambda tied to SQS, which is tied to the eleven-event-bus. posts filtered messages to associated dynamodb table (i.e. client-tracking) for consumption by ESImport.

## How to Build

To build the solution:

```bash
dotnet build rep-esimport-proxy.sln
```

## How to Deploy

Deploy ./pipeline/build-rep-esimport-proxy.yml to either the 'build' or 'build-staging' account, using the following command:


aws cloudformation create-stack --region us-west-2 --template-body file://pipeline/build-rep-esimport-proxy.yml --profile build --capabilities CAPABILITY_NAMED_IAM CAPABILITY_AUTO_EXPAND --stack-name build-rep-esimport-proxy


