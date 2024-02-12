### Pocket

### Prerequisites
You will need to create a consumer application which can be found on the [Pocket Developer Create a New App page](https://getpocket.com/developer/apps/new.php?). You should assign all three permission available: Add, Modify and Retrieve. For the platform, select the Web checkbox. Copy the consumer key, which will be used when creating a new connection for the connector.

### Instruction Steps
1. Create a new Power Platform custom connector called Pocket.
  - After creating the connector and the editor opens, click the toggle to open the Swagger editor and replace the entire connector template with the contents of apiProperties.swagger.json.
  - Go to the 5. Code tab, click the Upload button and select the script.csx file.
  - Save the connector.
  - Go to the 6. Test tab and create a New Connection. There is no authentication enabled in the Swagger as we will be using the two OAuth actions to retrieve an access token.
  - Update the connector.
2. You will now need to make two authentication actions before you can use the API actions. The first action 'Get request token' you will need to enter your consumer key. The action will response with two values, Request Token and ACTION_REQUIRED. The ACTION_REQUIRED value contains a URL you will need to enter in another browser window and approve the Pocket app using the request token. Once you have approved the app and the window returns to Power Automate, you can now call the 'Get access token' using your consumer key and request token. This action will now return the access token to use with any of the API actions.
