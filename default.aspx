<%@ Page Language="C#" CodeFile="WeMosDefGUI.aspx.cs" Inherits="WeMosDefGUI" AutoEventWireup="true" %>

<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
	<title>WeMosDef</title>
	<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-alpha.6/css/bootstrap.min.css" integrity="sha384-rwoIResjU2yc3z8GV/NPeZWAv56rSmLldC3R/AZzGRnGxQQKnKkoFVhFQhNUwEyJ" crossorigin="anonymous">
	
	<link href="dashboard.css" rel="stylesheet">
	<link href="main.css" rel="stylesheet">
</head>
<body>
	<nav class="navbar navbar-toggleable-md navbar-inverse fixed-top bg-inverse">
		<a class="navbar-brand" href="#">WeMosDef</a>
	</nav>

	<div class="container" style="margin: 34px 12px 12px 12px;">
		<div class="form-group" style="xwidth: 180px; text-align: center">
			<div class="alert alert-<%= (this.PowerState == "0" ? "danger" : "success") %>" role="alert">
				Power: <%= (this.PowerState == "0" ? "Off" : "On") %>
			</div>
		</div>
		<form action="" method="post" id="f1" name="f1">
			<input type="hidden" id="action" name="action" value="" />
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('on')">On</button>
			</div>
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('off')">Off</button>
			</div>
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('powerstate')">Power State</button>
			</div>
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="location.reload()">Reload</button>
			</div>
		</form>
	</div>

	<script src="https://code.jquery.com/jquery-3.1.1.slim.min.js" integrity="sha384-A7FZj7v+d/sdmMqp/nOQwliLvUsJfDHW+k9Omg/a/EheAdgtzNs3hpfag6Ed950n" crossorigin="anonymous"></script>
	<script src="https://cdnjs.cloudflare.com/ajax/libs/tether/1.4.0/js/tether.min.js" integrity="sha384-DztdAPBWPRXSA/3eYEEUWrWCy7G5KFbe8fFjk5JAIxUYHKkDx6Qin1DkWx51bBrb" crossorigin="anonymous"></script>
	<script src="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-alpha.6/js/bootstrap.min.js" integrity="sha384-vBWWzlZJ8ea9aCX4pEW3rVHjgjt7zpkNpZk+02D9phzyeVkE+jo0ieGizqPLForn" crossorigin="anonymous"></script>
	<script>
		function handleButton(action) {
			switch (action) {
				case 'on':
					break;
				case 'off':
					break;
				default:
					break;

			}
			$('#action').val(action);
			$('#f1').submit();
		}
	</script>

</body>
</html>
