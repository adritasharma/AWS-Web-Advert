# AWS-Web-Advert
Getting started with AWS. This project has been created as a practice along with this <a href="https://www.udemy.com/course/build-microservices-with-aspnet-core-amazon-web-services/">Udemy Course </a>

### Project Infastructure

The VPC (Private infastructure) is divided into 2 subnets.

- **Private Subnet** - (Not accessible from internet)
   This includes Microservice (Advert.API), Dynamo DB, Elastic Search (Search.API & Search.Worker-AWS Lambda ), SNS (Messaging)
    

- **Public Subnet** - (Accessible from internet)
     This includes AWS Cognito (Signup + Auth), S3 Bucket (Upload Image), CloudFront (Display Image - Caching image, css, fonts etc to user region ), Web UI (Cluster),


Advert API is placed in private subnet. It is not neded to be accessed from internet directly. Users can login through AWS Cognito (placed in public subnet)


### Working of Microservices in AWS

- We can deploy our .NET Core WebAPI Microservice either on  Amazon EC2 ( virtual server) or deploy it Amazon ECS (container service) using Docker and AWS App Mesh (Monitoring service)
- We can also use .NET Core to create lamda functions (serverless).
- We add Application Load Balancer infront of our EC2/ECS.
- Microservices should have data offloading, so insted of holding data in memory, they should put it in centralised cache (ElasticCache)
- Every Microservice should have it's own database. But creating different instance of database like SQL Server is costly, therefore we use Amazon RDS (Relation Database Service). For NoSql Database, we can use DynamoDB.
- Client (browser or mobile app) connect to Amazon CloudFront(Caching service). Cloudfront sends data to API Gateway (Aggragates all API into one address)


### Authentication and Authorization

**AWS Cognito** has all the following features:

- The applicatiion supports authentication through OAuth and OpenId Connect 
- It supports linking with Google and Facebook
- It is plugged into ASP.NET Core Identity
- It suupports token authentication (with JWT) as well as API authentication

**AWS Console Steps**

- Go to Service -> Cognito
- Create User pool and add attributes, change password policy, set verification rules, add App Clints like web client, ios client etc
- Create IAM user and attch policy **AmazonCognitoDeveloperAuthentication** and **AdministratorAccess**. Go to Security Credentials Tab and create Access key

**Windows System Steps**

- Create profile in our Windows System. Fo to Users root directory (type %USERPROFILE%). Create folder .aws.. Add file credentials

```
[default]
aws_access_key_id = XXXXXXXXXXXXXXXXXXXXXX
aws_secret_access_key = YYYYYYYYYYYYYYYYYY
```


_Note_ - **CQRS** (Command Query Responsibility Segregation) is an architectural pattern that separates reading and writing into two different models. It does responsibility segregation for the Command model & Query model. In our Architecture, **#Microservice 2 - Advert.API** is the Command Model (i.e writing Advertisements to database) and **#Microservice 4 - WebAdvert.SearchAPI** is for Query Model (Searching Advertisements for displaying)


## #Microservice 1 - WebAdvert.Web - This is our Web UI.

This is a ASP.NET Core MVC Web Application.

It has the following pages:

- SignUp, Login and Confirm Password pages which connects with AWS Cognito. AWS Nuget Packages has been used **Amazon.AspNetCore.Identity.Cognito** and **Amazon.Extensions.CognitoAuthentication**
- Advertisement Management page to create a new Advertisement (using #Microservice 2 - Advert.API) and s3 Bucket to upload image. AWS Nuget Packages **AWSSDK.S3** has been used.

```
     var bucketName = _configuration.GetValue<string>("ImageBucket");

            using (var client = new AmazonS3Client())
            {
                if (storageStream.Length > 0)
                    if (storageStream.CanSeek)
                        storageStream.Seek(0, SeekOrigin.Begin);

                var request = new PutObjectRequest
                {
                    AutoCloseStream = true,
                    BucketName = bucketName,
                    InputStream = storageStream,
                    Key = fileName
                };
                var response = await client.PutObjectAsync(request).ConfigureAwait(false);
                return response.HttpStatusCode == HttpStatusCode.OK;
    }
```

**AWS Console Steps for S3 Bucket**

- Go to Service -> Amazon S3
- Create a new bucket

## #Microservice 2 - Advert.API - This is the API to add Advertisements

This is a ASP.NET Core WebAPI Application. It connects with DynamoDB database.

**AWS Console Steps for DynamoDB**

- Go to Service -> DynamoDB
- Create a table( here : Adverts)


The Microservice has the following enpoints:

- An endpoint to add Advertisements. To connect with DynamoDB, AWS Nuget Package **AWSSDK.DynamoDBv2** has been used. In order to use DataModel with DynamoDB, some attributes from Amazon.DynamoDBv2.DataModel like [DynamoDBTable], [DynamoDBProperty] has been added to it.

```
using (var client = new AmazonDynamoDBClient())
{
   using (var context = new DynamoDBContext(client))
   {
      await context.SaveAsync(dbModel);
   }
}
```

When Advert API creates an advertisement in database, it sends a message (using **SNS**) to SearchWorker ( #Microservice 3), the SearchWorker creates a new document in **Elastic Search**. When user types for an Advertisement, it sends a request to  #Microservice 4 - WebAdvert.SearchAPI


**Messaging Concept**

- A message is a type of notification that a  microservice can send out.
- A message that is triggered when a state of the system changes is called an **Event**, eg AdvertisementCreated is raised when an advertisement is added to the database
- Messages can be directly sent to subscribers  (subscriber has to be available at that time) or can be placed in Queue, so that any subscriber to the message channel can poll (with a time interval)  and receive the message when the subscriber becomes available
- To implement messing in **AWS**, we can using **Simple Notification Service (SNS)** to send or receive message or **Simple Queue Service (SQS)** to persist the message  and subscribe it with polling.

**AWS Console Steps for SNS**

- Go to Service -> SNS
- Create a topic  (AdvertAPI Topic) , choose standard and keep the topic ARN


In Advert.Api, TopicArn is added in appsettings.json.  AWS Nuget Package **AWSSDK.SimpleNotificationService** is added

       using (var client = new AmazonSimpleNotificationServiceClient())
       {
             var message = new AdvertConfirmedMessage
             {
                  Id = model.Id,
                  Title = dbModel.Title
             };

             var messageJson = JsonConvert.SerializeObject(message);
             await client.PublishAsync(topicArn, messageJson);
       }


**Adding Health Check and Resilient Pattern to the Microservice**

This is added to check if the application is alive. We do it by using .Microsoft.AspNetCor.HealthChecks `AddHealthChecks` in startup.cs We have also added health check for individual service.

Exponential Backoff  and  Circuit Breaker has been added using Polly Library.



## #Microservice 3 - WebAdvert.SearchWorker - This is the AWS Lambda Function to pickup SNS messages and create document in Elastic Search

This is a AWS Lambda (Serverless Functions). This becomes available only when needed and thus saving the infastructure cost. AWS Lambda can be plugged into SNS directly to pickup messages and then act on it.

The SearchWorker creates a new document in **Elastic Search** whenever it gets a message from **SNS**

It is a Class Library .NET Core Project. AWS Nuget Packages **Amazon.Lambda.Core**, **Amazon.Lambda.SNSEvents** and **Amazon.Lambda.Serialization.Json** have been used for Lambda functionality. We can also install **Amazon.Lambda.Tools** to publish everything with .NET Cli. 

Nuget Package **NEST** is installed to work with Elastic Search



**AWS Console Steps for Elastic Search** - todo (31)

- Go to Service -> ElasticSearch
- Create a new domain (Elastic Search Domain is like container for our Elastic Search Instance)
- We chose Number of instance as 1 and Instance Type t2.small.elasticsearch
- We chose Number Storage Type EBS, EBS VolumeType Magnetic and size 10
- We chose Public access and somain template as Allow Open Access to the domain
- Copy the endpoint from Overview tab of the Elastic Search Domain created and add it to Search worker's appsettings.json todo (32)


        public SearchWorker(IElasticClient client)
        {
            _client = client;
        }
        public async Task Function(SNSEvent snsEvent, ILambdaContext context)
        {

            foreach (var record in snsEvent.Records)
            {
                context.Logger.LogLine(record.Sns.Message);

                var message = JsonConvert.DeserializeObject<AdvertConfirmedMessage>(record.Sns.Message);
                var advertDocument = MappingHelper.Map(message);
                await _client.IndexDocumentAsync(advertDocument);

            }
        }


### Uploading Lambda Function

**Packagaing WebAdvert.SearchWorker to a zip folder** - todo (31)

**AWS Console Steps**

  - We need to create a role for uploading Lambda Function. The role tells Amazon, what services this Lambda can access. Go to **IAM**, create new Role -> Choose Lambda -> Choose policy CloudWatchLogsFullAccess -> we can Add tag - Name: SearchWorkerRole -> Give Rolle name SearchWorkerRole and create role.
  - Go to Service -> Lambda -> Create Function -> Choose AuthorFromScratch -> Give a name :searchworker, select Runtime (eg: .NET Core 3.1), for Role - selct use existing role (SearchWorkerRole).
  - Go to the created SearchWorker Lambda -> Add Trigger -> Select SNS -> Choose AdvertAPI Topic ARN
  - Upload lambda code - todo (31)




