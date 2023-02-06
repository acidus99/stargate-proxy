certsDir = ~/kennedy-capsule/certs

prod: clean
	@dotnet publish -c Release -r osx-x64 --self-contained false
	@cp -R bin/Release/net6.0/osx-x64/publish/* output/
	@cp $(certsDir)/gemi.dev.* output/
	@cp *.sh output/
	@echo "Complete! Proxy binary and certs in 'output/'"

clean:
	@echo "Cleaning..."
	@rm -rf output/
	@mkdir output/
	@dotnet clean
	

