#!/bin/bash
if [ "$4" != "" ]; then
    DH_SERVICE_NAME=$1
    BITBUCKET_COMMIT=$2
    DH_DEV_DOCKER_REGISTRY=$3
    BITBUCKET_BRANCH=$4
    BITBUCKET_PROJECT_KEY=$5

    IMAGE_TAG=$(node -pe "require('./package.json')['version']")
    if [ "$BITBUCKET_BRANCH" = "lab" ] || [ "$BITBUCKET_BRANCH" = "develop" ]; then
        IMAGE_TAG="${IMAGE_TAG}-${BITBUCKET_COMMIT:0:7}"
    fi

    IMAGE_FULL_NAME=${DH_DEV_DOCKER_REGISTRY}/${DH_SERVICE_NAME}:${IMAGE_TAG}
    echo $IMAGE_FULL_NAME

    docker build --build-arg APP_BRANCH=$BITBUCKET_BRANCH --build-arg APP_NAME=$DH_SERVICE_NAME --build-arg APP_PROJ=$BITBUCKET_PROJECT_KEY --build-arg APP_VERSION=$IMAGE_TAG -t $IMAGE_FULL_NAME .
    docker push $IMAGE_FULL_NAME
    docker rmi $IMAGE_FULL_NAME
else
    echo "You need to input the module name and other 3 parameters!"
fi