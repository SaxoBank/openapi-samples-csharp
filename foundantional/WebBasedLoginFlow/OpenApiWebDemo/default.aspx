<%@ Page Language="C#" AutoEventWireup="true" CodeFile="default.aspx.cs" Inherits="OpenApiWebDemo.DefaultPage" %>

<!DOCTYPE html>

<html lang="en">
<head runat="server">
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<link rel="stylesheet" href="css/bootstrap.min.css" />
	<link rel="stylesheet" href="css/bootstrap-theme.min.css" />
	<title>Open API Demo</title>
</head>
<body>
	<nav class="navbar navbar-inverse">
		<div class="container">
			<div class="navbar-header">
				<button type="button" class="navbar-toggle collapsed" data-toggle="collapse" data-target="#navbar" aria-expanded="false" aria-controls="navbar">
					<span class="sr-only">Toggle navigation</span>
					<span class="icon-bar"></span>
					<span class="icon-bar"></span>
					<span class="icon-bar"></span>
				</button>
				<a class="navbar-brand" href="https://developer.saxobank.com/sim/openapi/portal/" target="_blank">OpenAPI</a>
			</div>
			<div id="navbar" class="navbar-collapse collapse">
				<ul class="nav navbar-nav">
					<li class="active"><a href="default.aspx">Login Sample</a></li>
				</ul>
			</div>
		</div>
	</nav>
	<div class="container">

		<div class="panel panel-primary">
			<div class="panel-heading">Server side request to Open API endpoint <code>/port/v1/clients/me</code> returned:</div>
			<div class="panel-body">
				<pre><%= OpenApiResponseData %></pre>
			</div>
		</div>

		<div class="panel panel-primary">
			<div class="panel-heading">
				Client side request to Open API endpoint <code>/port/v1/clients/me</code> returned:<br />
			</div>
			<div class="panel-body">
				<pre id="data"></pre>
				<a class="btn btn-primary" id="getData">Get data</a>
			</div>
		</div>

		<div class="panel panel-primary">
			<div class="panel-heading">
				AccessToken is:<br />
			</div>
			<div class="panel-body">
				<pre><%= TokenValue %></pre>
			</div>
		</div>
	<a href="refreshToken.aspx" class="btn btn-primary">Refresh Token</a>
	<a href="default.aspx" class="btn btn-primary">Reload Page</a>
	</div>

	<form style="display: none" id="samlForm" action="<%= AuthenticationUrl %>" method="POST">
		<input type="hidden" name="SAMLRequest" value="<%= SamlRequest %>" />
	</form>

	<script type="text/javascript" src="js/jquery-2.2.4.min.js"></script>
	<script type="text/javascript" src="js/bootstrap.min.js"></script>
	<script>
        <%= Script %>

		// Client side script for calling Open API
		var getClientsMe = function (baseUrl, token, element) {
			$.ajax({
				method: 'GET',
				url: baseUrl + '/port/v1/clients/me',
				headers: {
					'Authorization': token
				},
				dataType: 'text' // do not interpret the data - all we want to do is show it
			}).then(
                function (res) {
                	element.html(res);
                },
                function (err) {
                	element.html('An error occurred:' + err);
                }
            );
		};

		var baseUrl = '<%= OpenApiBaseUrl %>';
        var token = '<%= TokenType %> <%= TokenValue %>';

		$('#getData').on('click', function () {
			getClientsMe(baseUrl, token, $('#data'));
		});
	</script>
</body>
</html>
